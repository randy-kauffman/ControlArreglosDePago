using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Infraestructura;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Infraestructura
{
    /// <summary><c>ConversorDeEntidades</c> (diseno/03 4.4: Entity ⇄ diccionario neutro; <c>OptionSetValue</c>→<c>ValorDeOpcion</c>, <c>EntityReference</c>→<c>ReferenciaDeEntidad</c>, <c>Money</c>→<c>ValorDeDinero</c>).</summary>
    public class ConversorDeEntidadesAceptacion
    {
        // Valores de choice de los enums generados (conv. §6: nunca el literal).
        private const int Prestamo = (int)ProductoCrediticio.Prestamo;
        private const int Tarjeta = (int)ProductoCrediticio.Tarjeta;

        private static Entity UnaEntidad()
        {
            var id = Guid.NewGuid();
            var entidad = new Entity("sanic_carp_tbl_arreglo", id)
            {
                ["sanic_producto"] = new OptionSetValue(Prestamo),
                ["sanic_clienteid"] = new EntityReference("sanic_carp_tbl_cliente", Guid.NewGuid()) { Name = "ZZF Nombre" },
                ["sanic_monto"] = new Money(10.50m),
                ["sanic_saldoinicial"] = 1000.5000000000m,
                ["sanic_fechainicio"] = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Unspecified),
                ["sanic_fechacongelamiento"] = new DateTime(2026, 11, 5, 10, 30, 0, DateTimeKind.Utc),
                ["sanic_plazo"] = 6,
                ["sanic_comentarios"] = "ZZF-PRUEBA",
                ["sanic_correonuevoenfinalizada"] = true,
                ["sanic_otro"] = Guid.NewGuid(),
                ["sanic_nulo"] = null,
                ["sanic_carp_tbl_arregloid"] = id
            };
            entidad.RowVersion = "12345";
            return entidad;
        }

        [Fact]
        public void PA_CNV_01_ADiccionario_ConvierteLosTresTiposDelSdk_YDejaPasarElResto()
        {
            var entidad = UnaEntidad();

            var d = ConversorDeEntidades.ADiccionario(entidad);

            Assert.Equal(Prestamo, Assert.IsType<ValorDeOpcion>(d["sanic_producto"]).Valor);
            var referencia = Assert.IsType<ReferenciaDeEntidad>(d["sanic_clienteid"]);
            Assert.Equal("sanic_carp_tbl_cliente", referencia.Tabla);
            Assert.Equal(entidad.GetAttributeValue<EntityReference>("sanic_clienteid").Id, referencia.Id);
            Assert.Equal(10.50m, Assert.IsType<ValorDeDinero>(d["sanic_monto"]).Valor);
            Assert.Equal(1000.5000000000m, Assert.IsType<decimal>(d["sanic_saldoinicial"]));
            Assert.Equal(10, (decimal.GetBits((decimal)d["sanic_saldoinicial"])[3] >> 16) & 0xFF);     // la escala viaja intacta
            Assert.Equal(DateTimeKind.Unspecified, ((DateTime)d["sanic_fechainicio"]).Kind);
            Assert.Equal(DateTimeKind.Utc, ((DateTime)d["sanic_fechacongelamiento"]).Kind);
            Assert.Equal(6, d["sanic_plazo"]);
            Assert.Equal("ZZF-PRUEBA", d["sanic_comentarios"]);
            Assert.Equal(true, d["sanic_correonuevoenfinalizada"]);
            Assert.Equal(entidad["sanic_otro"], d["sanic_otro"]);
            Assert.True(d.ContainsKey("sanic_nulo") && d["sanic_nulo"] == null);                     // un null explícito se conserva (DC-05)
            Assert.Equal(entidad.Attributes.Count, d.Count);
        }

        [Fact]
        public void PA_CNV_02_ADiccionario_LosNombresSeComparanOrdinal_YElResultadoEsUnaCopia()
        {
            var entidad = UnaEntidad();

            var d = ConversorDeEntidades.ADiccionario(entidad);
            entidad["sanic_plazo"] = 99;
            entidad["sanic_nuevo"] = "x";

            Assert.False(d.ContainsKey("SANIC_PLAZO"));
            Assert.Equal(6, d["sanic_plazo"]);
            Assert.False(d.ContainsKey("sanic_nuevo"));
        }

        [Fact]
        public void PA_CNV_03_ADiccionario_NoTocaLaEntidadDeEntrada_YNulaLanza()
        {
            var entidad = UnaEntidad();
            var opcion = entidad.GetAttributeValue<OptionSetValue>("sanic_producto");
            var referencia = entidad.GetAttributeValue<EntityReference>("sanic_clienteid");

            ConversorDeEntidades.ADiccionario(entidad);

            Assert.Same(opcion, entidad["sanic_producto"]);
            Assert.Same(referencia, entidad["sanic_clienteid"]);
            Assert.Equal("ZZF Nombre", referencia.Name);
            Assert.Throws<ArgumentNullException>(() => ConversorDeEntidades.ADiccionario(null));
        }

        [Fact]
        public void PA_CNV_04_UnTipoQueNoEsDeLosTres_PasaTalCual_LaMismaInstancia()
        {
            // Los steps de Email leen Target["from"] (EntityCollection): el conversor no la toca.
            var partes = new EntityCollection(new List<Entity> { new Entity("activityparty") });
            var entidad = new Entity("email", Guid.NewGuid()) { ["from"] = partes, ["sanic_bytes"] = new byte[] { 1, 2 } };

            var d = ConversorDeEntidades.ADiccionario(entidad);

            Assert.Same(partes, d["from"]);
            Assert.Same(entidad["sanic_bytes"], d["sanic_bytes"]);
        }

        [Fact]
        public void PA_CNV_05_AFila_LlevaIdVersionYLosValoresSinElAtributoDelIdPrimario()
        {
            var entidad = UnaEntidad();

            var fila = ConversorDeEntidades.AFila(entidad);

            Assert.Equal(entidad.Id, fila.Id);
            Assert.Equal("12345", fila.VersionDeFila);
            Assert.False(fila.Valores.ContainsKey("sanic_carp_tbl_arregloid"));
            Assert.IsType<ValorDeOpcion>(fila.Valores["sanic_producto"]);
            Assert.False(fila.Valores.ContainsKey("SANIC_PRODUCTO"));
            Assert.Equal(entidad.Attributes.Count - 1, fila.Valores.Count);
        }

        [Fact]
        public void PA_CNV_06_AFila_EnEmailElIdPrimarioEsActivityid_YSinRowVersionLaVersionEsNula()
        {
            var id = Guid.NewGuid();
            var correo = new Entity("email", id) { ["activityid"] = id, ["subject"] = "ZZF-PRUEBA" };

            var fila = ConversorDeEntidades.AFila(correo);

            Assert.False(fila.Valores.ContainsKey("activityid"));
            Assert.Equal("ZZF-PRUEBA", fila.Valores["subject"]);
            Assert.Null(fila.VersionDeFila);
            Assert.Equal(id, fila.Id);
        }

        [Fact]
        public void PA_CNV_07_AEntidad_ConvierteLosTipos_DejaElNameNuloYPonerElId()
        {
            var id = Guid.NewGuid();
            var cliente = Guid.NewGuid();
            var valores = new Dictionary<string, object>
            {
                { "sanic_producto", new ValorDeOpcion(Tarjeta) },
                { "sanic_clienteid", new ReferenciaDeEntidad("sanic_carp_tbl_cliente", cliente) },
                { "sanic_monto", new ValorDeDinero(10.50m) },
                { "sanic_plazo", 6 },
                { "sanic_fecha", new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc) },
                { "sanic_nulo", null }
            };

            var entidad = ConversorDeEntidades.AEntidad("sanic_carp_tbl_arreglo", id, valores);

            Assert.Equal("sanic_carp_tbl_arreglo", entidad.LogicalName);
            Assert.Equal(id, entidad.Id);
            Assert.Equal(Tarjeta, entidad.GetAttributeValue<OptionSetValue>("sanic_producto").Value);
            var referencia = entidad.GetAttributeValue<EntityReference>("sanic_clienteid");
            Assert.Equal("sanic_carp_tbl_cliente", referencia.LogicalName);
            Assert.Equal(cliente, referencia.Id);
            Assert.Null(referencia.Name);
            Assert.Equal(10.50m, entidad.GetAttributeValue<Money>("sanic_monto").Value);
            Assert.Equal(6, entidad["sanic_plazo"]);
            Assert.Equal(DateTimeKind.Utc, entidad.GetAttributeValue<DateTime>("sanic_fecha").Kind);
            Assert.True(entidad.Attributes.ContainsKey("sanic_nulo") && entidad["sanic_nulo"] == null);
            Assert.Equal(valores.Count, entidad.Attributes.Count);
        }

        [Fact]
        public void PA_CNV_08_AEntidad_SinIdLaEntidadQuedaSinId_YUnValorYaDelSdkPasa()
        {
            var opcion = new OptionSetValue(Estados.StatusCorreoEnviado);

            var entidad = ConversorDeEntidades.AEntidad("email", Guid.Empty, new Dictionary<string, object> { { "statuscode", opcion } });

            Assert.Equal(Guid.Empty, entidad.Id);
            Assert.Equal(Estados.StatusCorreoEnviado, entidad.GetAttributeValue<OptionSetValue>("statuscode").Value);
        }

        [Fact]
        public void PA_CNV_09_AEntidad_ArgumentosInvalidos_Lanzan()
        {
            var valores = new Dictionary<string, object> { { "a", 1 } };

            Assert.Throws<ArgumentException>(() => ConversorDeEntidades.AEntidad(null, Guid.NewGuid(), valores));
            Assert.Throws<ArgumentException>(() => ConversorDeEntidades.AEntidad("", Guid.NewGuid(), valores));
            Assert.Throws<ArgumentNullException>(() => ConversorDeEntidades.AEntidad("email", Guid.NewGuid(), null));
            Assert.Throws<ArgumentException>(() => ConversorDeEntidades.AEntidad("email", Guid.NewGuid(), new Dictionary<string, object> { { "", 1 } }));
        }

        [Fact]
        public void PA_CNV_10_AEntidad_NoModificaElDiccionarioDeEntrada()
        {
            var opcion = new ValorDeOpcion(Tarjeta);
            var valores = new Dictionary<string, object> { { "sanic_producto", opcion } };

            ConversorDeEntidades.AEntidad("sanic_carp_tbl_arreglo", Guid.NewGuid(), valores);

            Assert.Single(valores);
            Assert.Same(opcion, valores["sanic_producto"]);
        }

        [Fact]
        public void PA_CNV_11_IdaYVuelta_ConservaLosValores()
        {
            var original = UnaEntidad();

            var vuelta = ConversorDeEntidades.AEntidad(original.LogicalName, original.Id, ConversorDeEntidades.ADiccionario(original));

            Assert.Equal(original.Attributes.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList(), vuelta.Attributes.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList());
            Assert.Equal(Prestamo, vuelta.GetAttributeValue<OptionSetValue>("sanic_producto").Value);
            Assert.Equal(original.GetAttributeValue<EntityReference>("sanic_clienteid").Id, vuelta.GetAttributeValue<EntityReference>("sanic_clienteid").Id);
            Assert.Equal(10.50m, vuelta.GetAttributeValue<Money>("sanic_monto").Value);
            Assert.Equal(original["sanic_saldoinicial"], vuelta["sanic_saldoinicial"]);
        }

        [Fact]
        public void PA_CNV_12_LosValoresSueltos_SeConvierten_YNullYLosPrimitivosPasanIguales()
        {
            Assert.Null(ConversorDeEntidades.AValorNeutro(null));
            Assert.Null(ConversorDeEntidades.AValorDelSdk(null));
            Assert.Equal(Tarjeta, Assert.IsType<ValorDeOpcion>(ConversorDeEntidades.AValorNeutro(new OptionSetValue(Tarjeta))).Valor);
            Assert.Equal(Tarjeta, Assert.IsType<OptionSetValue>(ConversorDeEntidades.AValorDelSdk(new ValorDeOpcion(Tarjeta))).Value);
            Assert.Equal(1.25m, Assert.IsType<Money>(ConversorDeEntidades.AValorDelSdk(new ValorDeDinero(1.25m))).Value);
            var id = Guid.NewGuid();
            Assert.Equal(id, Assert.IsType<ReferenciaDeEntidad>(ConversorDeEntidades.AValorNeutro(new EntityReference("team", id))).Id);
            foreach (var primitivo in new object[] { 5, "texto", true, 3.5m, id })
            {
                Assert.Equal(primitivo, ConversorDeEntidades.AValorNeutro(primitivo));
                Assert.Equal(primitivo, ConversorDeEntidades.AValorDelSdk(primitivo));
            }
        }
    }
}
