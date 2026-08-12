# OrderService — Microservicio de órdenes

Minimal API en ASP.NET Core 10 que genera órdenes desde el Basket existente, valida productos contra Catálogo y persiste documentos en MongoDB Atlas.

## Arquitectura

- `Domain`: entidades `Order`, `OrderItem`, estados y transición de ciclo de vida.
- `Application`: caso de uso, validaciones, cálculos e interfaces.
- `Infrastructure`: repositorio MongoDB y clientes HTTP de Basket/Catálogo.
- `Endpoints`: contrato HTTP Minimal API.
- `tests`: pruebas unitarias de reglas e idempotencia.

## Contrato

| Método | Ruta | Resultado |
|---|---|---|
| POST | `/api/orders` | Crea una orden; requiere `Idempotency-Key`. |
| GET | `/api/orders/{id}` | Recupera una orden. |
| GET | `/api/orders/customer/{customerId}` | Lista órdenes por cliente. |
| PATCH | `/api/orders/{id}/status` | Acepta `Confirmed` o `Cancelled` desde `Pending`. |
| GET | `/health` | Salud del servicio. |
| GET | `/swagger` | Swagger UI. |

`POST /api/orders` acepta `{ "customerId": "rafa", "basketId": "rafa" }`. Si se omite `basketId`, se utiliza `customerId`. El IVA es 16 %. Los precios se validan contra Catálogo y se congelan en la orden.

## MongoDB Atlas

1. Crear un clúster y un usuario de base de datos.
2. Autorizar la IP de desarrollo y la salida de Render (para una práctica puede usarse `0.0.0.0/0` con contraseña robusta).
3. Copiar `.env.example` a un almacenamiento local seguro y definir `MongoDB__ConnectionString`.
4. No confirmar `.env`, connection strings ni contraseñas en Git.

El servicio crea la colección `orders` y dos índices: uno único para `IdempotencyKey` y otro para consultas por cliente/fecha.

## Ejecución local

En PowerShell:

```powershell
$env:MongoDB__ConnectionString='mongodb+srv://...'
$env:MongoDB__DatabaseName='eshop-orders'
$env:MongoDB__CollectionName='orders'
$env:Services__BasketUrl='https://eshop-servicess.onrender.com/'
$env:Services__CatalogUrl='https://catalogapi-4t24.onrender.com/'
$env:ASPNETCORE_URLS='http://localhost:8084'
dotnet run --project src\OrderService.Api
```

`launchSettings.json`, Vite y la documentación usan el mismo puerto: `8084`. Luego abrir
`http://localhost:8084/swagger`. El frontend usa `/api/orders`, que Vite redirige a ese puerto.
Basket y Catálogo se consumen directamente en sus URLs de Render porque sus proyectos locales
no forman parte de este workspace. Pueden sobrescribirse mediante `Services__BasketUrl` y
`Services__CatalogUrl` si posteriormente se ejecutan copias locales reales.

`GET /health` realiza un `ping` real a MongoDB y verifica los índices. Una respuesta `200` incluye
`"mongodb": "Connected"`; por tanto, no es un health check estático.

## Pruebas

```powershell
dotnet test OrderService.slnx
```

`OrderService.http` contiene las peticiones para capturar evidencias de creación, consulta, idempotencia y estados. Para Basket vacío, usar un cliente sin productos. Para MongoDB no disponible, usar temporalmente una cadena inválida y comprobar que el cliente recibe un Problem Details sin stack trace.

### Flujo de prueba P1-P8

1. P1: guardar un Basket con productos y crear la orden; esperar `201` y el documento en Atlas.
2. P2: copiar el folio en `OrderService.http` y consultar por ID; esperar `200`.
3. P3: usar un cliente sin Basket; esperar `400`.
4. P4: repetir el POST con la misma `Idempotency-Key`; esperar `200` y el mismo folio.
5. P5: cambiar una orden nueva de `Pending` a `Confirmed`; esperar `200`.
6. P6: intentar otro cambio desde `Confirmed`; esperar `400`.
7. P7: arrancar temporalmente con Mongo no disponible; esperar `503` sin stack trace.
8. P8: realizar la compra desde React; comprobar folio, estado `Pending` y total.

## Publicación en Render

1. Crear un **Web Service** desde el repositorio y seleccionar Docker.
2. Configurar el Dockerfile raíz y puerto `8084`.
3. Agregar las variables de `.env.example`, incluyendo la cadena privada de Atlas.
4. Configurar `Cors__AllowedOrigins__1` con la URL real de Netlify.
5. Verificar `/health` y `/swagger`.

También puede utilizarse el Blueprint `render.yaml`. Render solicitará manualmente
`MongoDB__ConnectionString` y `Cors__AllowedOrigins__1`; esos valores nunca se guardan en Git.

Después de obtener la URL pública, agregar a `E-Shop-Frontend-Completo/public/_redirects`, antes de la regla SPA:

```text
/api/orders/*  https://TU-ORDER-SERVICE.onrender.com/api/orders/:splat  200
/api/orders    https://TU-ORDER-SERVICE.onrender.com/api/orders         200
```

Volver a ejecutar `npm run build` y desplegar `dist` en Netlify.

## Idempotencia y errores

Una clave repetida devuelve la orden existente con `200 OK`; una nueva devuelve `201 Created`. El índice único evita duplicados incluso ante concurrencia. Las reglas de negocio responden `400`, recursos ausentes `404`, dependencias HTTP no disponibles `503` y errores inesperados `500`, sin exponer secretos ni stack traces.

Los estados se serializan como texto (`Pending`, `Confirmed`, `Cancelled`) tanto en Swagger como en las respuestas JSON. Consulta `EVIDENCIAS.md` para los resultados P1-P8 ejecutados contra Atlas.
