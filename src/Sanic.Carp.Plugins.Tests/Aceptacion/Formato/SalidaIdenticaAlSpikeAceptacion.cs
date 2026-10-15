using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// PA-GEN-27 (real) (diseno/03 1.1, línea 327): salida idéntica a la del spike 1b para 6 y 60 cuotas, con los sellos de fecha y hora DOS de
    /// <c>[Content_Types].xml</c> comparados en cero (texto corregido el 2026-10-14 por decisión del usuario; antes pedía 6, 30 y 60 cuotas).
    /// <para>
    /// <b>Lo que se puede medir hoy.</b> El spike dejó dos salidas, <c>spikes/plantilla/salida-ejemplo-6-cuotas.xlsx</c> y
    /// <c>salida-ejemplo-60-cuotas.xlsx</c> (las que escribe <c>tools/Medicion</c> con datos ficticios), copiadas a <c>Fixtures/Spike1b/</c>.
    /// Los datos y el mapeo de aquí son los de <c>tools/Medicion/Program.cs</c>, línea por línea; el esperado es el archivo del spike, que NO sale de este
    /// código. <b>NO existe una salida del spike para 30 cuotas</b>: el valor esperado no se inventa ni se calcula con el generador bajo prueba;
    /// el caso de 30 cuotas no se escribe: el usuario aceptó el 2026-10-14 comparar solo 6 y 60 cuotas, y el texto de PA-GEN-27 se corrigió. Tampoco hay una salida del spike con la celda de
    /// <c>moneda</c> (la v07 no la imprime, OA-7): la clave libre <c>moneda</c> sin celda mapeada no cambia la salida, y eso lo fija
    /// <c>MonedaEnElGeneradorAceptacion</c>.
    /// </para>
    /// <para>
    /// <b>"Byte a byte", exactamente.</b> Medido el 2026-10-14: los 24 componentes del paquete (mismos nombres, mismo orden, mismo método de
    /// compresión, mismo contenido) y el archivo entero salen iguales, SALVO la fecha y hora DOS que el SDK de Open XML estampa en la entrada
    /// <c>[Content_Types].xml</c> al escribirla (la hora de la generación: 4 bytes en el encabezado local y 4 en el directorio central). Un
    /// archivo que lleva la hora de su generación no puede ser idéntico a otro generado otro día, así que esta prueba compara el archivo
    /// COMPLETO con esos campos de fecha y hora de cada entrada puestos en cero, y además cada componente por separado para que un
    /// fallo diga cuál cambió. Es una tensión del diseño ("byte a byte" literal es inalcanzable sin que el generador fije los sellos de
    /// tiempo del ZIP), documentada en el informe del lote 3.
    /// </para>
    /// Mutación que mata: cualquier cambio del generador que altere un byte de la salida para estos datos (orden de partes, estilos, cadenas
    /// compartidas, fórmulas, <c>calcChain</c>, <c>fullCalcOnLoad</c>, método de compresión, un atributo de XML). <b>VERDE A PROPÓSITO</b>: el generador
    /// del lote 2 ya la cumple; fija que no se pierda.
    /// </summary>
    public class SalidaIdenticaAlSpikeAceptacion
    {
        private static byte[] SalidaDelSpike(string nombre)
        {
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Spike1b", nombre));
        }

        /// <summary>Datos ficticios de <c>tools/Medicion/Program.cs</c>: con <paramref name="conPagos"/> las primeras 12 cuotas traen pago (la 12 parcial).</summary>
        private static DatosArreglo DatosDelSpike(int plazo, bool conPagos)
        {
            var inicio = new DateTime(2026, 11, 8);
            decimal cuota = plazo == 60 ? 2150.35m : 4200.10m;
            var cuotas = new List<CuotaDato>();
            for (int i = 1; i <= plazo; i++)
            {
                var fecha = inicio.AddMonths(i - 1);
                bool pagada = conPagos && i <= 12;
                cuotas.Add(new CuotaDato(i, fecha, cuota, pagada ? fecha.AddDays(1) : (DateTime?)null, pagada ? (i == 12 ? cuota / 2 : cuota) : (decimal?)null));
            }
            var total = cuotas.Sum(c => c.Monto);
            var valores = new Dictionary<string, object>
            {
                { "fechaFormalizacion", new DateTime(2026, 10, 8) }, { "oficial", "Oficial de Prueba" },
                { "clienteNombre", "Ana Lucia Martinez Ruiz (ficticio)" }, { "clienteCedula", "000-000000-0000X" },
                { "clienteCorreo", "cliente.ficticio@ejemplo.com" }, { "clienteCelular", "0000-0000" },
                { "numeroProducto", "0000-0000-0000-0000" }, { "tipo", "Temporal" }, { "numeroArreglo", 3 },
                { "saldoInicial", 125000.75m }, { "montoAbonado", 5000m }, { "saldoRemanente", 120000.75m },
                { "plazo", plazo }, { "cuota", cuota }, { "totalPagar", total },
                { "fechaInicio", inicio }, { "fechaFin", inicio.AddMonths(plazo - 1) }, { "cuentaDebitar", "0000000000000000" },
                { "comentarios", "Datos ficticios del spike C-01. Plazo de " + plazo + " cuotas." },
            };
            return new DatosArreglo(valores, plazo, cuotas);
        }

        private static ushort U16(byte[] b, int posicion)
        {
            return (ushort)(b[posicion] | (b[posicion + 1] << 8));
        }

        private static uint U32(byte[] b, int posicion)
        {
            return (uint)(b[posicion] | (b[posicion + 1] << 8) | (b[posicion + 2] << 16) | (b[posicion + 3] << 24));
        }

        /// <summary>Copia del ZIP con la fecha y la hora DOS de cada entrada (encabezado local y directorio central) puestas en cero.</summary>
        private static byte[] SinSellosDeTiempo(byte[] zip)
        {
            var copia = (byte[])zip.Clone();
            int fin = -1;
            for (int i = zip.Length - 22; i >= 0 && fin < 0; i--)
            {
                if (U32(zip, i) == 0x06054b50u) fin = i;
            }
            Assert.True(fin >= 0, "No se encontró el registro de fin de directorio central del ZIP.");
            int entradas = U16(zip, fin + 10);
            int posicion = (int)U32(zip, fin + 16);
            for (int n = 0; n < entradas; n++)
            {
                Assert.Equal(0x02014b50u, U32(zip, posicion));
                Array.Clear(copia, posicion + 12, 4);
                int local = (int)U32(zip, posicion + 42);
                Assert.Equal(0x04034b50u, U32(zip, local));
                Array.Clear(copia, local + 10, 4);
                posicion += 46 + U16(zip, posicion + 28) + U16(zip, posicion + 30) + U16(zip, posicion + 32);
            }
            return copia;
        }

        private static List<KeyValuePair<string, byte[]>> Componentes(byte[] zip)
        {
            var lista = new List<KeyValuePair<string, byte[]>>();
            using (var archivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
            {
                foreach (var entrada in archivo.Entries)
                {
                    using (var lectura = entrada.Open())
                    using (var memoria = new MemoryStream())
                    {
                        lectura.CopyTo(memoria);
                        lista.Add(new KeyValuePair<string, byte[]>(entrada.FullName, memoria.ToArray()));
                    }
                }
            }
            return lista;
        }

        [Theory]
        [InlineData(6, "salida-ejemplo-6-cuotas.xlsx", false)]
        [InlineData(60, "salida-ejemplo-60-cuotas.xlsx", true)]
        public void PA_GEN_27_ElGeneradorRealDaLaMismaSalidaQueElSpike1b_ComponenteAComponenteYByteAByteSalvoLosSellosDeTiempoDelZip(int plazo, string salidaDelSpike, bool conPagos)
        {
            var plantilla = Plantillas.V07;
            var generador = new GeneradorOpenXml(null);

            var resultado = generador.Generar(plantilla, Plantillas.Sha256(plantilla), Plantillas.MapaV07(), DatosDelSpike(plazo, conPagos));

            var esperada = SalidaDelSpike(salidaDelSpike);
            var esperadosComponentes = Componentes(esperada);
            var obtenidosComponentes = Componentes(resultado.Archivo);
            Assert.Equal(esperadosComponentes.Select(c => c.Key).ToArray(), obtenidosComponentes.Select(c => c.Key).ToArray());
            for (int i = 0; i < esperadosComponentes.Count; i++)
            {
                Assert.True(esperadosComponentes[i].Value.SequenceEqual(obtenidosComponentes[i].Value),
                    "El componente '" + esperadosComponentes[i].Key + "' de la salida de " + plazo + " cuotas difiere del spike 1b.");
            }
            Assert.Equal(esperada.Length, resultado.Archivo.Length);
            Assert.True(SinSellosDeTiempo(esperada).SequenceEqual(SinSellosDeTiempo(resultado.Archivo)),
                "Los componentes coinciden pero el archivo completo (sin sellos de tiempo) difiere del spike 1b: cambió el empaquetado del ZIP (compresión, orden o metadatos).");
        }
    }
}
