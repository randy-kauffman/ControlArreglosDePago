using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <c>ColumnasDeFecha</c> (diseno/03 4.5): qué columnas son "solo fecha" (fecha DO) y cuáles "fecha y hora, usuario local" (UL), porque la
    /// plataforma devuelve las primeras con <c>Kind=Unspecified</c> a las 00:00 y las segundas con <c>Kind=Utc</c> (fixtures de Dev, 2026-10-14). La lista
    /// se contrasta con el diccionario de datos: si el modelo suma una columna de solo fecha y la lista no, esta prueba falla (D-63 de M-PPP, DC-26).
    /// <b>VERDE A PROPÓSITO</b>: la lista es dato escrito por el arquitecto; la prueba impide que se desactualice.
    /// </summary>
    public class ColumnasDeFechaAceptacion
    {
        [Fact]
        public void PA_FEC_01_LaListaDeSoloFechaEsExactamenteLaDelDiccionarioDeDatos_VERDE_A_PROPOSITO()
        {
            var delDiccionario = new List<string>();
            foreach (var par in DiccionarioDeDatos.Leer())
            {
                foreach (var columna in par.Value.Where(c => c.Clase == ClaseDeColumna.FechaSolo)) delDiccionario.Add(par.Key + "." + columna.Nombre);
            }

            var delDoble = ColumnasDeFecha.SoloFecha.Select(p => p.Key + "." + p.Value).ToList();

            Assert.Equal(delDiccionario.OrderBy(x => x, StringComparer.Ordinal).ToArray(), delDoble.OrderBy(x => x, StringComparer.Ordinal).ToArray());
            Assert.NotEmpty(delDoble);
        }

        [Theory]
        [InlineData("sanic_carp_tbl_arreglo", "sanic_fechainicio", true)]
        [InlineData("sanic_carp_tbl_arreglo", "sanic_fechafin", true)]
        [InlineData("sanic_carp_tbl_arreglo", "sanic_fechaformalizacion", true)]
        [InlineData("sanic_carp_tbl_cuota", "sanic_fechavencimiento", true)]
        [InlineData("sanic_carp_tbl_cuota", "sanic_fechapago", true)]
        [InlineData("sanic_carp_tbl_arreglo", "sanic_fechacongelamiento", false)]
        [InlineData("sanic_carp_tbl_conversacion", "sanic_fechaprimercorreo", false)]
        [InlineData("email", "actualend", false)]
        [InlineData("sanic_carp_tbl_cuota", "sanic_fechainicio", false)]
        [InlineData("sanic_carp_tbl_arreglo", "SANIC_FECHAINICIO", false)]
        [InlineData(null, "sanic_fechainicio", false)]
        [InlineData("sanic_carp_tbl_arreglo", null, false)]
        public void PA_FEC_02_EsSoloFechaEsPorTablaYColumnaOrdinal_VERDE_A_PROPOSITO(string tabla, string columna, bool esperado)
        {
            Assert.Equal(esperado, ColumnasDeFecha.EsSoloFecha(tabla, columna));
        }
    }
}
