using System.Globalization;
using OrderService.Api.Application;
using OrderService.Api.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OrderService.Api.Infrastructure;

public sealed class OrderPdfGenerator : IOrderPdfGenerator
{
    private static readonly CultureInfo MexicanCulture = CultureInfo.GetCultureInfo("es-MX");
    private const string AccentColor = "#CFFF2E";
    private const string DarkColor = "#16201D";

    public byte[] Generate(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(10).FontColor(DarkColor));

                page.Header().Column(header =>
                {
                    header.Spacing(4);
                    header.Item().Text("E-SHOP SERVICES").Bold().FontSize(22).FontColor(DarkColor);
                    header.Item().Text("ORDEN DE COMPRA").Bold().FontSize(15).FontColor("#587000");
                    header.Item().PaddingTop(6).LineHorizontal(3).LineColor(AccentColor);
                });

                page.Content().PaddingVertical(20).Column(content =>
                {
                    content.Spacing(14);
                    content.Item().Table(details =>
                    {
                        details.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });
                        Detail(details, "Folio", order.Id);
                        Detail(details, "Cliente", order.CustomerId);
                        Detail(details, "Fecha", order.CreatedAt.ToString("dd/MM/yyyy hh:mm tt", MexicanCulture));
                        Detail(details, "Estado", order.Status.ToString());
                        if (!string.IsNullOrWhiteSpace(order.BasketId))
                        {
                            Detail(details, "Basket", order.BasketId);
                            details.Cell();
                        }
                    });

                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(5);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Producto");
                            header.Cell().Element(HeaderCell).Text("Cant.");
                            header.Cell().Element(HeaderCell).Text("P. unitario");
                            header.Cell().Element(HeaderCell).Text("Importe");
                        });

                        foreach (var item in order.Items)
                        {
                            BodyCell(table, item.ProductName);
                            BodyCell(table, item.Quantity.ToString(MexicanCulture), alignRight: true);
                            BodyCell(table, Money(item.UnitPrice), alignRight: true);
                            BodyCell(table, Money(item.LineTotal), alignRight: true);
                        }
                    });

                    content.Item().AlignRight().Width(235).Table(totals =>
                    {
                        totals.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.ConstantColumn(110);
                        });
                        TotalRow(totals, "Subtotal", order.Subtotal);
                        TotalRow(totals, "IVA", order.Tax);
                        totals.Cell().PaddingTop(8).BorderTop(2).BorderColor(DarkColor)
                            .Text("TOTAL").Bold().FontSize(12);
                        totals.Cell().PaddingTop(8).BorderTop(2).BorderColor(DarkColor).AlignRight()
                            .Text(Money(order.Total)).Bold().FontSize(12);
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Documento generado por OrderService · Página ");
                    text.CurrentPageNumber();
                });
            });
        }).GeneratePdf();
    }

    private static string Money(decimal value) => value.ToString("C2", MexicanCulture);

    private static void Detail(TableDescriptor table, string label, string value)
    {
        table.Cell().PaddingVertical(3).Text(text =>
        {
            text.Span($"{label}: ").SemiBold();
            text.Span(value);
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.Background(DarkColor).Padding(8).DefaultTextStyle(style => style.Bold().FontColor(Colors.White));

    private static void BodyCell(TableDescriptor table, string text, bool alignRight = false)
    {
        var cell = table.Cell().BorderBottom(1).BorderColor("#DDE3DB").PaddingVertical(8).PaddingHorizontal(6);
        if (alignRight) cell = cell.AlignRight();
        cell.Text(text);
    }

    private static void TotalRow(TableDescriptor table, string label, decimal value)
    {
        table.Cell().PaddingVertical(3).Text(label);
        table.Cell().PaddingVertical(3).AlignRight().Text(Money(value));
    }
}
