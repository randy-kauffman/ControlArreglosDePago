using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// Decisiones del arquitecto, 2026-10-12 (diseno/03 4.3 puntos 11 y 14).
    /// <para>
    /// Punto 11: el tope de tamaño descomprimido (50 MB) se mide sobre los bytes REALMENTE leídos de cada entrada, no sobre el tamaño que
    /// declara el directorio central (BP-PP-062: el costo del parser queda atado a lo configurado, no a lo que dice el archivo). Un
    /// archivo hostil declara poco y ocupa mucho. <c>ZipArchive</c> no deja fabricarlo, así que <see cref="ZipFabricado"/> escribe el
    /// ZIP a mano, con entradas almacenadas (método 0): en .NET una entrada almacenada se lee completa aunque su tamaño declarado
    /// mienta (solo las entradas deflate se recortan al tamaño declarado), de modo que la prueba es igual en net8.0 y en net462.
    /// </para>
    /// <para>
    /// Punto 14: toda falla del contenedor queda dentro de la frontera. Como el generador lee cada entrada para medirla, una entrada
    /// ilegible rechaza la plantilla con <see cref="PlantillaInvalidaException"/>, y cualquier excepción al verificar el ZIP (no solo
    /// <c>InvalidDataException</c>) sale igual (A-17). En net8.0 todas las fallas fabricables son <c>InvalidDataException</c>; en net462
    /// la BCL puede lanzar también <c>IOException</c> o <c>NotSupportedException</c>, y eso queda cubierto por la regla, no por una fila.
    /// </para>
    /// </summary>
    public class TopeDelContenedorAceptacion
    {
        private const string ParteGrande = "customXml/item2.xml";
        private const int Mega = 1024 * 1024;

        private static Exception Generar(byte[] plantilla, int plazo = 6)
        {
            return Record.Exception(() =>
                GeneradorFormato.Generar(plantilla, Plantillas.Sha256(plantilla), Plantillas.MapaV07(), Plantillas.Datos(plazo)));
        }

        private static byte[] ConParteDeMegas(string parte, int megas, uint? declarado)
        {
            return ZipFabricado.Con(Plantillas.V07, l =>
            {
                var e = ZipFabricado.Buscar(l, parte);
                e.Datos = new byte[megas * Mega];
                e.TamanoDeclarado = declarado;
            });
        }

        // ---- Punto 11 ----

        [Fact]
        public void Generar_UnaParteQueDeclaraPocoYOcupaMasQueElTope_LaRechazaPorSuTamanoReal()
        {
            var plantilla = ConParteDeMegas(ParteGrande, 52, 1024);        // declara 1 KB, ocupa 52 MB (tope: 50 MB)

            var ex = Generar(plantilla);

            Assert.IsType<PlantillaInvalidaException>(ex);
            Assert.Contains("tamaño", ex.Message);
        }

        [Fact]
        public void Generar_VariasPartesQueDeclaranPocoYSumanMasQueElTope_LasRechazaPorLaSumaReal()
        {
            // Ninguna pasa sola de 50 MB (11 MB cada una); entre las cinco suman 55 MB. Todas declaran 10 bytes.
            var plantilla = ZipFabricado.Con(Plantillas.V07, l =>
            {
                foreach (var parte in new[] { "customXml/item1.xml", "customXml/item2.xml", "xl/printerSettings/printerSettings1.bin",
                                              "xl/printerSettings/printerSettings2.bin", "xl/media/image1.png" })
                {
                    var e = ZipFabricado.Buscar(l, parte);
                    e.Datos = new byte[11 * Mega];
                    e.TamanoDeclarado = 10;
                }
            });

            var ex = Generar(plantilla);

            Assert.IsType<PlantillaInvalidaException>(ex);
            Assert.Contains("tamaño", ex.Message);
        }

        /// <summary>VERDE A PROPÓSITO: el mismo ZIP con tamaños HONESTOS ya se rechaza hoy; prueba que el tamaño de la fila anterior es lo único que la separa.</summary>
        [Fact]
        public void Generar_UnaParteAlmacenadaConTamanoHonestoMayorQueElTope_LaRechaza()
        {
            var plantilla = ConParteDeMegas(ParteGrande, 52, null);

            var ex = Generar(plantilla);

            Assert.IsType<PlantillaInvalidaException>(ex);
            Assert.Contains("tamaño", ex.Message);
        }

        /// <summary>
        /// VERDE A PROPÓSITO: control del apoyo. Un ZIP escrito a mano con tamaños honestos y poco contenido genera igual que el original;
        /// sin esto, un error del apoyo se confundiría con un rechazo legítimo.
        /// </summary>
        [Fact]
        public void Generar_ZipEscritoAManoConTamanosHonestos_GeneraIgualQueElOriginal()
        {
            var aMano = ZipFabricado.Con(Plantillas.V07, l => { });

            var esperado = Paquete.Abrir(GeneradorFormato.Generar(Plantillas.V07, Plantillas.Sha256(Plantillas.V07), Plantillas.MapaV07(), Plantillas.Datos(6)));
            var salida = Paquete.Abrir(GeneradorFormato.Generar(aMano, Plantillas.Sha256(aMano), Plantillas.MapaV07(), Plantillas.Datos(6)));

            foreach (var celda in new[] { "B8", "F7", "D12", "B13" })
            {
                Assert.Equal(esperado.Valor(Plantillas.HojaFormato, celda), salida.Valor(Plantillas.HojaFormato, celda));
            }
            Assert.Equal("Ana Lucía Martínez Ruiz", salida.Valor(Plantillas.HojaFormato, "B8"));
        }

        // ---- Punto 14 ----

        private static byte[] ConEntradaDañada(Action<EntradaZip> dañar)
        {
            return ZipFabricado.Con(Plantillas.V07, l => dañar(ZipFabricado.Buscar(l, "customXml/item2.xml")));
        }

        // Una entrada que el libro no usa, pero que el generador lee para medirla: si no se puede leer, la plantilla no sirve.
        // Las filas "(verde)" ya se rechazan hoy (el SDK recorre todas las partes al guardar y la envoltura final las convierte);
        // las demás (método de compresión desconocido, deflate corrupto) pasan sin error porque hoy nadie lee la entrada.
        public static TheoryData<string, byte[]> EntradasIlegibles()
        {
            return new TheoryData<string, byte[]>
            {
                { "método de compresión que ningún lector admite", ConEntradaDañada(e => e.Metodo = 99) },
                { "método LZMA, que la BCL no implementa", ConEntradaDañada(e => e.Metodo = 14) },
                { "deflate con datos corruptos", ConEntradaDañada(e => { e.Metodo = 8; e.Datos = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }; }) },
                { "tamaño comprimido declarado mayor que el archivo (verde)", ConEntradaDañada(e => e.TamanoComprimidoDeclarado = 900000000u) },
                { "tamaño comprimido declarado de 4 GB (verde)", ConEntradaDañada(e => e.TamanoComprimidoDeclarado = 0xFFFFFFFFu) },
                { "encabezado local fuera del archivo (verde)", ConEntradaDañada(e => e.PosicionLocalDeclarada = 0x7FFFFFFFu) },
                { "encabezado local que apunta a basura (verde)", ConEntradaDañada(e => e.PosicionLocalDeclarada = 5u) },
            };
        }

        [Theory]
        [MemberData(nameof(EntradasIlegibles))]
        public void Generar_EntradaIlegibleDentroDeUnContenedorQueAbre_DaPlantillaInvalida(string caso, byte[] plantilla)
        {
            var ex = Generar(plantilla);

            Assert.True(ex is PlantillaInvalidaException, "[" + caso + "] se esperaba PlantillaInvalidaException y fue " + (ex == null ? "ninguna excepción" : ex.GetType().FullName + ": " + ex.Message));
            Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        }

        // VERDE A PROPÓSITO: el contenedor roto desde el directorio central ya se convierte hoy (InvalidDataException del constructor).
        public static TheoryData<string, byte[]> ContenedoresRotos()
        {
            var bueno = ZipFabricado.Con(Plantillas.V07, l => { });
            Func<Action<byte[]>, byte[]> alterar = cambio => { var b = (byte[])bueno.Clone(); cambio(b); return b; };
            var corto = new byte[bueno.Length - 10];
            Array.Copy(bueno, corto, corto.Length);
            return new TheoryData<string, byte[]>
            {
                { "registro de fin de directorio cortado", corto },
                { "posición del directorio central fuera del archivo", alterar(b => BitConverter.GetBytes(0x7FFFFFFFu).CopyTo(b, b.Length - 6)) },
                { "cantidad de entradas que no coincide con el directorio", alterar(b => BitConverter.GetBytes((ushort)0xFFFF).CopyTo(b, b.Length - 12)) },
                { "archivo dividido (disco distinto de cero)", alterar(b => BitConverter.GetBytes((ushort)7).CopyTo(b, b.Length - 18)) },
            };
        }

        [Theory]
        [MemberData(nameof(ContenedoresRotos))]
        public void Generar_ContenedorConElDirectorioCentralRoto_DaPlantillaInvalida(string caso, byte[] plantilla)
        {
            var ex = Generar(plantilla);

            Assert.True(ex is PlantillaInvalidaException, "[" + caso + "] se esperaba PlantillaInvalidaException y fue " + (ex == null ? "ninguna excepción" : ex.GetType().FullName + ": " + ex.Message));
        }
    }
}
