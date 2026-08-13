# Evidencias de pruebas P1-P9

Fecha de ejecución: 12 de agosto de 2026. Entorno: OrderService local, MongoDB Atlas y servicios Basket/Catálogo de Render.

## Resultado

| Prueba | Estado | Evidencia obtenida |
|---|---|---|
| P1 Crear orden válida | PASS | `201 Created`; folio `0847ea09f3c94b9e94a76a86d42f46d1`; subtotal 3999, IVA 639.84 y total 4638.84. |
| P2 Consultar orden | PASS | `GET /api/orders/{id}` devolvió `200 OK` con cliente, fecha, estado, items y totales. |
| P3 Basket vacío | PASS | `400 Bad Request`: `El Basket está vacío o no existe.` |
| P4 Repetir Idempotency-Key | PASS | `200 OK`, mismo folio y una sola orden. También se repitió la solicitud React después de eliminar el Basket y devolvió el mismo folio. |
| P5 Pending a Confirmed | PASS | `200 OK`; orden `0847ea09f3c94b9e94a76a86d42f46d1` quedó `Confirmed`. |
| P6 Transición inválida | PASS | `Confirmed -> Cancelled` y `Cancelled -> Confirmed` devolvieron `400 Bad Request`. |
| P7 MongoDB no disponible | PASS | Proceso aislado en 8085 devolvió `503`: `MongoDB no respondió dentro del tiempo esperado.` Sin stack trace ni credenciales. |
| P8 Flujo React | PASS | Botón `Realizar compra`, confirmación visible y orden `fa2bd36847a44ae1b67bf0c3d112d126` con estado `Pending` y total 4638.84. Basket eliminado después del éxito. |

## Evidencia P9 - Reporte PDF

Estado: PASS.

```text
Orden creada
  -> GET /api/orders/{id}/pdf
  -> 200 OK
  -> Content-Type: application/pdf
  -> Content-Disposition: inline
  -> PDF visible con folio, cliente, fecha, estado, items y totales
```

- `Generate_supports_multiple_items` genera filas para Teclado, Mouse y Monitor.
- Las pruebas generan documentos para los `CustomerId` reales `rafa` y
  `codex-render-smoke-20260812`, sin nombres hardcodeados en el generador.
- Una orden inexistente produce `ResourceNotFoundException`, traducida por el middleware actual a
  `404 Not Found`.
- El documento se genera como `byte[]` en memoria y comienza con la firma válida `%PDF-`.
- El endpoint usa el snapshot persistido y no consulta nuevamente Basket ni Catálogo.

## Evidencia P10 - Trazabilidad BasketId

- Basket identifica el carrito con `userName`; OrderService consulta `GET basket/{basketId}`.
- Las órdenes nuevas guardan esa clave real como `BasketId`, con fallback compatible a
  `CustomerId` cuando el request anterior no lo envía.
- `GET /api/orders/{id}` y `GET /api/orders/customer/{customerId}` exponen `BasketId`.
- Idempotencia conserva el mismo `BasketId` y no crea una segunda orden.
- Las transiciones `Pending -> Confirmed` y `Pending -> Cancelled` siguen funcionando; las
  transiciones posteriores continúan rechazadas.
- Una prueba BSON deserializa una orden histórica sin `BasketId` y genera su PDF sin errores.
- Suite final: 18 pruebas aprobadas.

### Validación local final

- Orden real creada para `Jhony Bravo` con `BasketId: Jhony Bravo`.
- Estado `Pending`, 2 productos, subtotal 6999, IVA 1119.84 y total 8118.84.
- PDF generado por Minimal API con folio, cliente, Basket, productos y totales.

## Comprobaciones adicionales

- `GET /health`: `200 OK` con `mongodb: Connected`; ejecuta un ping real a Atlas.
- `GET /api/orders/customer/rafa`: devolvió las órdenes persistidas.
- `Pending -> Cancelled`: `200 OK` para la orden `5204356dd2574bf4bcf5dfe9e5f02005`.
- Estados JSON serializados como `Pending`, `Confirmed` y `Cancelled`.
- Minimal API confirmada en `Endpoints/OrderEndpoints.cs` mediante `MapPost`, `MapGet` y `MapPatch`.
- El frontend conserva la misma `Idempotency-Key` al reintentar la misma composición de carrito.

## Puertos y dependencias comprobados

| Componente | Dirección |
|---|---|
| Frontend Vite | `http://localhost:5173` |
| OrderService | `http://localhost:8084` |
| Basket | `https://eshop-servicess.onrender.com/` |
| Catálogo | `https://catalogapi-4t24.onrender.com/` |
| MongoDB | Atlas, base `eshop-orders`, colección `orders` |
