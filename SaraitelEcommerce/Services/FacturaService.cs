using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Saraitel.API.Interfaces;
using Saraitel.API.Models;
using Saraitel.Entidades;

namespace Saraitel.API.Services
{
    // HU-17: arma la factura en PDF de un pedido pagado (QuestPDF).
    // Diseño: franja negra arriba y abajo, cuerpo blanco y productos
    // separados solo por espacio, sin tabla.
    public class FacturaService : IFacturaService
    {
        private static readonly CultureInfo Colombia = new CultureInfo("es-CO");

        // Colores
        private const string Negro = "#000000";
        private const string Blanco = "#FFFFFF";
        private const string Texto = "#111111";
        private const string TextoSecundario = "#333333";
        private const string Gris = "#8A8A8A";
        private const string GrisClaro = "#CFCFCF";

        // Imágenes de la factura (se copian a la carpeta de salida al compilar)
        private static readonly string CarpetaRecursos =
            Path.Combine(AppContext.BaseDirectory, "Recursos");

        public byte[] GenerarPdf(Pedido pedido, string correoCliente)
        {
            return Document.Create(documento =>
            {
                documento.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4);
                    pagina.Margin(0);
                    pagina.PageColor(Blanco);
                    pagina.DefaultTextStyle(estilo => estilo.FontSize(9.5f).FontColor(Texto));

                    pagina.Header().Element(c => Encabezado(c, pedido));
                    pagina.Content().Element(c => Contenido(c, pedido, correoCliente));
                    pagina.Footer().Element(Pie);
                });
            }).GeneratePdf();
        }

        /* ---------- Franja negra de arriba ---------- */

        private static void Encabezado(IContainer contenedor, Pedido pedido)
        {
            contenedor.MinHeight(150).Layers(capas =>
            {
                // Fondo negro con líneas diagonales
                capas.Layer().Image(Recurso("factura-encabezado.png")).FitUnproportionally();

                capas.PrimaryLayer().PaddingVertical(30).PaddingHorizontal(40).Row(fila =>
                {
                    // Logo y datos del vendedor
                    fila.RelativeItem().Column(columna =>
                    {
                        columna.Item().Width(170).Image(Recurso("logo-saraitel-blanco.png")).FitWidth();

                        columna.Item().PaddingTop(12)
                            .Text(DatosEmpresa.RazonSocial.ToUpper())
                            .FontSize(8).Bold().FontColor(Blanco).LetterSpacing(0.08f);

                        columna.Item().PaddingTop(2)
                            .Text($"NIT {DatosEmpresa.Nit}")
                            .FontSize(8).FontColor(GrisClaro);
                    });

                    // Título, número y fecha
                    fila.RelativeItem().Column(columna =>
                    {
                        columna.Item().AlignRight()
                            .Text("FACTURA")
                            .FontSize(34).Black().FontColor(Blanco).LetterSpacing(0.08f);

                        DatoEncabezado(columna, "Factura: ", $"N° {pedido.NumeroFactura}");
                        DatoEncabezado(columna, "Fecha: ", FechaLarga(pedido.FechaFactura ?? DateTime.Now).ToUpper());
                        DatoEncabezado(columna, "Pedido: ", $"#{pedido.IdPedido}");
                    });
                });
            });
        }

        private static void DatoEncabezado(ColumnDescriptor columna, string etiqueta, string valor)
        {
            columna.Item().PaddingTop(5).AlignRight().Text(texto =>
            {
                texto.Span(etiqueta).FontSize(8.5f).FontColor(GrisClaro);
                texto.Span(valor).FontSize(8.5f).Bold().FontColor(Blanco).LetterSpacing(0.04f);
            });
        }

        /* ---------- Cuerpo blanco ---------- */

        private static void Contenido(IContainer contenedor, Pedido pedido, string correoCliente)
        {
            var envio = pedido.Envio ?? new DatosEnvio();
            var facturacion = pedido.Facturacion ?? envio;

            contenedor.PaddingHorizontal(40).PaddingTop(26).PaddingBottom(20).Column(columna =>
            {
                // Cliente (datos de facturación) y envío
                columna.Item().Row(fila =>
                {
                    fila.RelativeItem().Column(c =>
                    {
                        Titulo(c, "INFORMACIÓN DEL CLIENTE");
                        Dato(c, "NOMBRE:", $"{facturacion.Nombre} {facturacion.Apellidos}");
                        Dato(c, "DOCUMENTO:", $"{DatosEnvio.Abreviatura(facturacion.TipoDocumento)} {facturacion.Cedula}");
                        Dato(c, "TELÉFONO:", facturacion.Telefono);
                        Dato(c, "CORREO:", correoCliente);
                        Dato(c, "DIRECCIÓN:", Direccion(facturacion, false));
                    });

                    fila.ConstantItem(30);

                    fila.RelativeItem().Column(c =>
                    {
                        Titulo(c, "ENVÍO");
                        Dato(c, "RECIBE:", $"{envio.Nombre} {envio.Apellidos}");
                        Dato(c, "DIRECCIÓN:", Direccion(envio, true));
                        Dato(c, "TELÉFONO:", envio.Telefono);
                        Dato(c, "MÉTODO:", pedido.MetodoEnvio);
                    });
                });

                // Productos
                columna.Item().PaddingTop(24).Element(c => Productos(c, pedido));

                // Pago y totales
                columna.Item().PaddingTop(22).LineHorizontal(0.6f).LineColor(GrisClaro);

                columna.Item().PaddingTop(14).Row(fila =>
                {
                    fila.RelativeItem().Column(c =>
                    {
                        Titulo(c, "INFORMACIÓN DE PAGO");
                        Dato(c, "MEDIO:", $"{pedido.MedioPago} (Wompi)");
                        Dato(c, "TRANSACCIÓN:", pedido.IdTransaccion);
                        Dato(c, "REFERENCIA:", pedido.ReferenciaPago);
                        Dato(c, "PAGADO EL:", pedido.FechaPago.HasValue
                            ? pedido.FechaPago.Value.ToString("d 'de' MMMM 'de' yyyy, h:mm tt", Colombia)
                            : "");
                    });

                    fila.ConstantItem(30);

                    fila.ConstantItem(230).Column(c => Totales(c, pedido));
                });

                // Contacto y agradecimiento
                columna.Item().PaddingTop(26).Row(fila =>
                {
                    fila.RelativeItem().Column(c =>
                    {
                        Titulo(c, "CONTACTO");
                        c.Item().Text(DatosEmpresa.Direccion);
                        c.Item().Text($"Tel. {DatosEmpresa.Telefono} · {DatosEmpresa.TelefonoSecundario}");
                        c.Item().Text(DatosEmpresa.Correo);
                    });

                    fila.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("¡GRACIAS POR TU COMPRA!")
                            .FontSize(15).Black().LetterSpacing(0.03f);
                        c.Item().PaddingTop(4).AlignRight().Text("Saraitel Comunicaciones")
                            .FontColor("#555555");
                    });
                });

                columna.Item().PaddingTop(18)
                    .Text("Documento de prueba generado en ambiente Sandbox — no válido como " +
                          "factura electrónica DIAN. Los precios incluyen IVA.")
                    .FontSize(7).FontColor(Gris);
            });
        }

        // Lista de productos: una sola línea bajo los títulos y cada producto
        // separado por espacio en blanco, sin renglones ni bordes
        private static void Productos(IContainer contenedor, Pedido pedido)
        {
            contenedor.Column(columna =>
            {
                columna.Item().BorderBottom(1.5f).BorderColor(Negro).PaddingBottom(6).Row(fila =>
                {
                    fila.RelativeItem().Text("DESCRIPCIÓN").FontSize(8).Black().LetterSpacing(0.08f);
                    fila.ConstantItem(85).AlignRight().Text("PRECIO").FontSize(8).Black().LetterSpacing(0.08f);
                    fila.ConstantItem(70).AlignCenter().Text("CANTIDAD").FontSize(8).Black().LetterSpacing(0.08f);
                    fila.ConstantItem(90).AlignRight().Text("TOTAL").FontSize(8).Black().LetterSpacing(0.08f);
                });

                foreach (var detalle in pedido.Detalles)
                {
                    columna.Item().PaddingTop(9).Row(fila =>
                    {
                        fila.RelativeItem().Column(c =>
                        {
                            c.Item().Text(detalle.Nombre);
                            c.Item().PaddingTop(1)
                                .Text($"Código SRT-{detalle.IdProducto:D4}")
                                .FontSize(7.5f).FontColor(Gris);
                        });

                        fila.ConstantItem(85).AlignRight().Text(Moneda(detalle.PrecioUnitario));
                        fila.ConstantItem(70).AlignCenter().Text(detalle.Cantidad.ToString());
                        fila.ConstantItem(90).AlignRight().Text(Moneda(detalle.Subtotal)).Bold();
                    });
                }
            });
        }

        private static void Totales(ColumnDescriptor columna, Pedido pedido)
        {
            int articulos = pedido.Detalles.Sum(d => d.Cantidad);

            LineaTotal(columna, $"Subtotal · {articulos} {(articulos == 1 ? "artículo" : "artículos")}",
                pedido.Subtotal, false);
            LineaTotal(columna, "Envío estándar", pedido.CostoEnvio, false);

            // Los precios ya incluyen IVA: aquí solo se desglosa
            LineaTotal(columna, "Base sin IVA (productos)", pedido.Subtotal - pedido.Impuestos, true);
            LineaTotal(columna, $"IVA {Pedido.TasaIva * 100:0} % (incluido)", pedido.Impuestos, true);

            columna.Item().PaddingTop(8).Background(Negro)
                .PaddingVertical(8).PaddingHorizontal(10).Row(fila =>
                {
                    fila.RelativeItem().Text("TOTAL")
                        .FontSize(12).Black().FontColor(Blanco).LetterSpacing(0.06f);
                    fila.AutoItem().Text(Moneda(pedido.TotalPedido))
                        .FontSize(12).Black().FontColor(Blanco);
                });
        }

        private static void LineaTotal(ColumnDescriptor columna, string etiqueta, decimal valor, bool suave)
        {
            string color = suave ? Gris : TextoSecundario;
            float tamano = suave ? 8.5f : 9.5f;

            columna.Item().PaddingBottom(4).Row(fila =>
            {
                fila.RelativeItem().Text(etiqueta).FontSize(tamano).FontColor(color);
                fila.AutoItem().Text(Moneda(valor)).FontSize(tamano).FontColor(color);
            });
        }

        /* ---------- Franja negra de abajo ---------- */

        private static void Pie(IContainer contenedor)
        {
            contenedor.MinHeight(46).Layers(capas =>
            {
                capas.Layer().Image(Recurso("factura-pie.png")).FitUnproportionally();

                capas.PrimaryLayer().MinHeight(46).PaddingHorizontal(40).AlignMiddle().Row(fila =>
                {
                    fila.RelativeItem()
                        .Text($"{DatosEmpresa.RazonSocial.ToUpper()} · NIT {DatosEmpresa.Nit}")
                        .FontSize(7.5f).FontColor(Blanco).LetterSpacing(0.06f);

                    fila.AutoItem().Text(texto =>
                    {
                        texto.Span("Página ").FontSize(7.5f).FontColor(Blanco);
                        texto.CurrentPageNumber().FontSize(7.5f).FontColor(Blanco);
                        texto.Span(" de ").FontSize(7.5f).FontColor(Blanco);
                        texto.TotalPages().FontSize(7.5f).FontColor(Blanco);
                    });
                });
            });
        }

        /* ---------- Ayudas ---------- */

        private static void Titulo(ColumnDescriptor columna, string texto)
        {
            columna.Item().PaddingBottom(8).Text(texto).FontSize(11).Black().LetterSpacing(0.06f);
        }

        // Una línea "ETIQUETA:  valor"
        private static void Dato(ColumnDescriptor columna, string etiqueta, string? valor)
        {
            columna.Item().PaddingBottom(3).Row(fila =>
            {
                fila.ConstantItem(72).PaddingTop(1).Text(etiqueta).FontSize(8).Bold().LetterSpacing(0.03f);
                fila.RelativeItem().Text(string.IsNullOrWhiteSpace(valor) ? "—" : valor)
                    .FontColor(TextoSecundario);
            });
        }

        private static string Direccion(DatosEnvio datos, bool conCodigoPostal)
        {
            string linea1 = string.IsNullOrWhiteSpace(datos.Complemento)
                ? datos.Direccion
                : $"{datos.Direccion}, {datos.Complemento}";

            string linea2 = $"{datos.Ciudad}, {datos.Departamento}";

            if (conCodigoPostal && !string.IsNullOrWhiteSpace(datos.CodigoPostal))
                linea2 += $" · {datos.CodigoPostal}";

            return $"{linea1}\n{linea2}";
        }

        // $ 365.000 (pesos colombianos, sin decimales)
        private static string Moneda(decimal valor)
        {
            return "$ " + valor.ToString("N0", Colombia);
        }

        private static string FechaLarga(DateTime fecha)
        {
            return fecha.ToString("d 'de' MMMM 'de' yyyy", Colombia);
        }

        private static string Recurso(string nombre)
        {
            return Path.Combine(CarpetaRecursos, nombre);
        }
    }
}
