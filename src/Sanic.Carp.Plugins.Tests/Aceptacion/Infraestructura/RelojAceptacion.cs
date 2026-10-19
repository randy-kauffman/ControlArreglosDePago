using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Sanic.Carp.Plugins.Infraestructura;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Infraestructura
{
    /// <summary><c>Reloj</c> (diseno/03 4.2 y 0.6: la hora entra por <c>IReloj</c>; en producción es <c>DateTime.UtcNow</c>).</summary>
    public class RelojAceptacion
    {
        // La prueba SÍ lee el reloj del sistema: es la única forma de comprobar que Reloj lo hace. La regla de 0.6 rige para el código del ensamblado.
        [Fact]
        public void PA_REL_01_AhoraUtc_EsElInstanteActualConKindUtc()
        {
            var reloj = new Reloj();

            var antes = DateTime.UtcNow;
            var leido = reloj.AhoraUtc;
            var despues = DateTime.UtcNow;

            Assert.Equal(DateTimeKind.Utc, leido.Kind);
            Assert.InRange(leido.Ticks, antes.Ticks, despues.Ticks);
        }

        [Fact]
        public void PA_REL_02_NoCongelaLaHora_CadaLecturaEsNueva_YLasInstanciasNoComparteEstado()
        {
            var uno = new Reloj();
            var otro = new Reloj();

            var primera = uno.AhoraUtc;
            Thread.Sleep(30);
            var segunda = otro.AhoraUtc;
            var tercera = uno.AhoraUtc;

            Assert.True(segunda - primera >= TimeSpan.FromMilliseconds(20), "La hora no avanza: el reloj la guardó.");
            Assert.True(tercera >= segunda);
        }

        // La guardia mira todo el ensamblado menos Infraestructura/Reloj.cs, y ve el código sin comentarios. Tolera espacios (DateTime . UtcNow),
        // el calificador (System.DateTime.UtcNow) y también DateTimeOffset. Una variante con otro nombre no la engaña: lo que se prohíbe es leer el reloj.
        private static readonly Regex LecturaDelReloj = new Regex(
            @"\b(?:DateTime|DateTimeOffset)\s*\.\s*(?:Now|UtcNow|Today)\b",
            RegexOptions.CultureInvariant);

        [Fact]
        public void PA_REL_03_NingunArchivoDelEnsambladoLeeLaHoraDelSistema_SalvoReloj()
        {
            // VERDE A PROPÓSITO: fija la defensa de 0.6 ("nada de DateTime.Now/UtcNow fuera de Reloj"). Hoy pasa porque el stub de Reloj no lee nada; si el constructor
            // la lee en otro archivo (o en un helper), falla.
            var infractores = FuentesDe(string.Empty)
                .Where(f => !string.Equals(f.Key, "Infraestructura/Reloj.cs", StringComparison.Ordinal))
                .Where(f => LecturaDelReloj.IsMatch(f.Value))
                .Select(f => f.Key)
                .ToList();

            Assert.True(infractores.Count == 0, "Leen la hora del sistema fuera de Reloj: " + string.Join(", ", infractores));
        }

        [Fact]
        public void PA_REL_04_ElDetectorReconoceLasFormasDeLeerElReloj_YNoLosComentarios()
        {
            // Mata una guardia que no ve "DateTime . UtcNow", "System.DateTime.Now" o "DateTimeOffset.UtcNow".
            // VERDE A PROPÓSITO: fija el detector de PA_REL_03.
            var prohibidas = new[] { "var a = DateTime.UtcNow;", "var a = DateTime . Now;", "var a = System.DateTime.Today;", "var a = DateTimeOffset.UtcNow;" };
            // Medir una duración (Stopwatch, TickCount) no es leer la hora: la regla de 0.6 habla de la fecha y la hora del dominio.
            var permitidas = new[] { "var t = Stopwatch.StartNew();", "var t = Environment.TickCount;", "var a = reloj.AhoraUtc;", "var a = MiDateTime.UtcNowValue;", "var a = DateTime_UtcNow;", "var a = new DateTime(2026, 1, 1);" };

            Assert.All(prohibidas, t => Assert.True(LecturaDelReloj.IsMatch(t), t));
            Assert.All(permitidas, t => Assert.False(LecturaDelReloj.IsMatch(t), t));
        }
    }
}
