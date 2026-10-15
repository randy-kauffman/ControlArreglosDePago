using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoMedido;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <b>Lote 3b: borrado por relación</b> (<c>salida/20-borrado-por-relacion.json</c>, punto (15); diccionario 6.1 y 6.2). El doble deja de lanzar <see cref="NotSupportedException"/> y emula el
    /// comportamiento que el diccionario declara: <b>Restrict</b> (<c>restringido</c>): el <c>Delete</c> del padre falla <c>0x80040227</c> nombrando la relación y cada hija que restringe, y no se borra
    /// NADA (ni las hijas Cascade del mismo padre); <b>Cascade</b> (<c>parental</c>): las hijas dejan de existir; <b>RemoveLink</b> (<c>quitar_vinculo</c>, REL-09): la hija sobrevive y su lookup queda
    /// AUSENTE; <b>NAT-01</b> (Regarding de <c>email</c> a Conversación): borrar la conversación borra sus correos.
    /// <para>
    /// <b>REL-10 a REL-19</b> (padre <c>systemuser</c>, declaradas Restrict, NO MEDIDAS: no se crean ni borran usuarios): se conserva el <see cref="NotSupportedException"/> del lote 3 nombrando la relación
    /// (CL3_H1_01, restringida a esas diez). <b>Límites declarados:</b> el Cascade y el RemoveLink no disparan steps ni figuran en <c>Escrituras</c> (no son escrituras del código; si la plataforma corre los
    /// steps de las hijas borradas, no se midió) y el orden de las líneas de un mensaje Restrict con varias hijas no se garantiza.
    /// </para>
    /// </summary>
    public class MedidoEnDevRelacionesAceptacion
    {
        private static readonly Dictionary<string, string> TablaDelNombreVisible = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Cliente", Cliente }, { "Conversación", Conversacion }, { "Arreglo", Arreglo }, { "Cuota", Cuota }, { "Versión de plantilla", VersionPlantilla },
            { "Mapeo de celda", Mapeo }, { "Generación de formato", Generacion }, { "Usuario", "systemuser" }, { "Email (estándar)", Correo }
        };

        /// <summary>(id, nombre, padre, hija, lookup, comportamiento) de la sección 6.2, leídas por esta prueba (no por el doble).</summary>
        private static IEnumerable<string[]> Leer()
        {
            var en62 = false;
            foreach (var linea in ApoyoDeAceptacion.LineasDelDocumento("diseno/02-diccionario-datos.md"))
            {
                if (linea.StartsWith("### 6.2", StringComparison.Ordinal)) { en62 = true; continue; }
                if (linea.StartsWith("### 6.3", StringComparison.Ordinal)) break;
                if (!en62 || !linea.StartsWith("| REL-", StringComparison.Ordinal)) continue;
                var celdas = linea.Split('|').Select(c => c.Trim()).ToArray();
                yield return new[] { celdas[1], celdas[2].Trim('`'), TablaDelNombreVisible[celdas[3]], TablaDelNombreVisible[celdas[4]], celdas[5].Trim('`'), celdas[6].Replace("*", string.Empty) };
            }
        }

        private static IEnumerable<object[]> Filtradas(string comportamiento)
        {
            foreach (var r in Leer().Where(r => r[5] == comportamiento && r[2] != "systemuser")) yield return new object[] { r[0], r[1], r[2], r[3], r[4] };
        }

        public static IEnumerable<object[]> Restringidas() { return Filtradas("restringido"); }

        public static IEnumerable<object[]> Parentales() { return Filtradas("parental"); }

        public static IEnumerable<object[]> QuitarVinculo() { return Filtradas("quitar_vinculo"); }

        [Fact]
        public void PA_MED_REL_00_ElDiccionarioDeclara5RestringidasSinUsuario_3Parentales_Y1QuitarVinculo_YDiezConPadreUsuario_VERDE_A_PROPOSITO()
        {
            // Guarda: si el diccionario cambia, las teorías de abajo no pueden quedarse cortas en silencio. Medidas: REL-01 a 04 y 07 (Restrict), REL-05, 06 y 08 (Cascade), REL-09 (RemoveLink).
            Assert.Equal(new[] { "REL-01", "REL-02", "REL-03", "REL-04", "REL-07" }, Restringidas().Select(o => (string)o[0]).ToArray());
            Assert.Equal(new[] { "REL-05", "REL-06", "REL-08" }, Parentales().Select(o => (string)o[0]).ToArray());
            Assert.Equal(new[] { "REL-09" }, QuitarVinculo().Select(o => (string)o[0]).ToArray());
            Assert.Equal(10, Leer().Count(r => r[2] == "systemuser"));
        }

        [Theory]
        [MemberData(nameof(Restringidas))]
        public void PA_MED_REL_01_RestrictElDeleteDelPadreConUnaHijaFalla0x80040227_NombraLaRelacionYLaHija_YNoBorraNada(string id, string nombre, string padre, string hija, string lookup)
        {
            // 20-borrado-por-relacion.json REL-01, 02, 03, 04, 07: 'The object you tried to delete is associated with another object and cannot be deleted ... Restricting entity <hija> ... has Id: ...
            // and is collected by Relationship with name: <relación>'. Mata: el NotSupportedException del lote 3, o borrar el padre.
            var almacen = new OrganizationServiceEnMemoria();
            var idDelPadre = Sembrada(almacen, padre);
            var idDeLaHija = Sembrada(almacen, hija, lookup, new EntityReference(padre, idDelPadre));

            var falla = Falla(() => almacen.Delete(padre, idDelPadre));

            AssertFalla(falla, CodigosDeFalla.RestriccionDeRelacion, "The object you tried to delete is associated with another object and cannot be deleted.", id);
            Assert.Contains("Deleting records from " + padre, falla.Detail.Message, StringComparison.Ordinal);
            Assert.Contains("Restricting entity " + hija, falla.Detail.Message, StringComparison.Ordinal);
            Assert.Contains("has Id: " + idDeLaHija, falla.Detail.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("is collected by Relationship with name: " + nombre + ".", falla.Detail.Message, StringComparison.Ordinal);
            Assert.True(almacen.Existe(padre, idDelPadre) && almacen.Existe(hija, idDeLaHija), "no se borró nada");
            Assert.Empty(almacen.Escrituras);
        }

        [Fact]
        public void PA_MED_REL_02_ElMensajeRestrictTraeUnaLineaPorHija_DeCadaRelacionQueRestringe()
        {
            // 20-borrado-por-relacion.json REL-04: 'un renglón por hija'. Un cliente con dos conversaciones y un arreglo nombra REL-01 dos veces y REL-02 una.
            var almacen = new OrganizationServiceEnMemoria();
            var cliente = Sembrada(almacen, Cliente);
            var c1 = Sembrada(almacen, Conversacion, "sanic_clienteid", new EntityReference(Cliente, cliente));
            var c2 = Sembrada(almacen, Conversacion, "sanic_clienteid", new EntityReference(Cliente, cliente));
            var a1 = Sembrada(almacen, Arreglo, "sanic_clienteid", new EntityReference(Cliente, cliente));

            var falla = Falla(() => almacen.Delete(Cliente, cliente));

            var lineas = falla.Detail.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Where(l => l.StartsWith("Restricting entity", StringComparison.Ordinal)).ToArray();
            Assert.Equal(3, lineas.Length);
            Assert.Equal(2, lineas.Count(l => l.Contains("sanic_carp_cliente_conversacion", StringComparison.Ordinal)));
            Assert.Equal(1, lineas.Count(l => l.Contains("sanic_carp_cliente_arreglo", StringComparison.Ordinal)));
            foreach (var hija in new[] { c1, c2, a1 }) Assert.Contains(hija.ToString(), falla.Detail.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void PA_MED_REL_03_UnPadreConHijasRestrictYCascadeFallaYNoBorraNiLasHijasCascade()
        {
            // 20-borrado-por-relacion.json REL-07: la Versión con Generaciones (Restrict) y el Mapeo (Cascade): falla, y el Mapeo sigue.
            var almacen = new OrganizationServiceEnMemoria();
            var version = Sembrada(almacen, VersionPlantilla);
            var generacion = Sembrada(almacen, Generacion, "sanic_versionplantillaid", new EntityReference(VersionPlantilla, version));
            var mapeo = Sembrada(almacen, Mapeo, "sanic_versionplantillaid", new EntityReference(VersionPlantilla, version));

            var falla = Falla(() => almacen.Delete(VersionPlantilla, version));

            AssertFalla(falla, CodigosDeFalla.RestriccionDeRelacion, "sanic_carp_versionplantilla_generacionformato");
            Assert.DoesNotContain("sanic_carp_versionplantilla_mapeocelda", falla.Detail.Message, StringComparison.Ordinal);
            Assert.True(almacen.Existe(VersionPlantilla, version) && almacen.Existe(Generacion, generacion) && almacen.Existe(Mapeo, mapeo));
        }

        [Theory]
        [MemberData(nameof(Parentales))]
        public void PA_MED_REL_04_CascadeElDeleteDelPadreBorraLasHijas_YSoloLasDeEsePadre(string id, string nombre, string padre, string hija, string lookup)
        {
            // 20-borrado-por-relacion.json REL-05, 06, 08: 'existen_despues': false para el padre y la hija. Mata: el NotSupportedException o dejar hijas huérfanas.
            var almacen = new OrganizationServiceEnMemoria();
            var idDelPadre = Sembrada(almacen, padre);
            var otroPadre = Sembrada(almacen, padre);
            var hijas = new[] { Sembrada(almacen, hija, lookup, new EntityReference(padre, idDelPadre)), Sembrada(almacen, hija, lookup, new EntityReference(padre, idDelPadre)) };
            var deOtro = Sembrada(almacen, hija, lookup, new EntityReference(padre, otroPadre));

            almacen.Delete(padre, idDelPadre);

            Assert.False(almacen.Existe(padre, idDelPadre), id + ": el padre");
            foreach (var borrada in hijas) Assert.False(almacen.Existe(hija, borrada), id + ": la hija");
            Assert.True(almacen.Existe(hija, deOtro) && almacen.Existe(padre, otroPadre), id + ": lo de otro padre sigue");
            Assert.Equal(new[] { OperacionDeDatos.Delete }, almacen.Escrituras.Select(e => e.Operacion).ToArray());
        }

        [Theory]
        [MemberData(nameof(QuitarVinculo))]
        public void PA_MED_REL_05_RemoveLinkElDeleteDelPadreDejaLaHijaViva_ConElLookupAusente(string id, string nombre, string padre, string hija, string lookup)
        {
            // 20-borrado-por-relacion.json REL-09: se borra la conversación a la que apunta sanic_unidaaid; la otra sigue y su sanic_unidaaid queda ausente.
            var almacen = new OrganizationServiceEnMemoria();
            var unida = Sembrada(almacen, padre);
            var otraConversacion = Sembrada(almacen, hija, lookup, new EntityReference(padre, unida), "sanic_asunto", "ZZF-PRUEBA");
            var versionAntes = almacen.Leer(hija, otraConversacion).RowVersion;

            almacen.Delete(padre, unida);

            Assert.False(almacen.Existe(padre, unida));
            var viva = almacen.Leer(hija, otraConversacion);
            Assert.False(viva.Contains(lookup), id + ": el lookup debe quedar ausente");
            Assert.Equal("ZZF-PRUEBA", viva["sanic_asunto"]);
            Assert.NotEqual(versionAntes, viva.RowVersion);
        }

        [Fact]
        public void PA_MED_REL_06_NAT01_BorrarLaConversacionBorraSusCorreosDeRegarding()
        {
            // 20-borrado-por-relacion.json NAT-01: 'existen_despues': conversación false, email false (Cascade, diccionario 6.3). Los correos de otra conversación siguen.
            var almacen = new OrganizationServiceEnMemoria();
            var conversacion = Sembrada(almacen, Conversacion);
            var otra = Sembrada(almacen, Conversacion);
            var correo = Sembrada(almacen, Correo, "regardingobjectid", new EntityReference(Conversacion, conversacion));
            var correoDeOtra = Sembrada(almacen, Correo, "regardingobjectid", new EntityReference(Conversacion, otra));

            almacen.Delete(Conversacion, conversacion);

            Assert.False(almacen.Existe(Correo, correo));
            Assert.True(almacen.Existe(Correo, correoDeOtra) && almacen.Existe(Conversacion, otra));
        }

        [Fact]
        public void PA_MED_REL_07_UnaConversacionConUnCorreoYUnArregloQueLaReferenciaFallaPorElRestrictYNoBorraElCorreo()
        {
            // REL-03 (Restrict) y NAT-01 (Cascade) a la vez: manda el Restrict y el correo sigue.
            var almacen = new OrganizationServiceEnMemoria();
            var conversacion = Sembrada(almacen, Conversacion);
            var correo = Sembrada(almacen, Correo, "regardingobjectid", new EntityReference(Conversacion, conversacion));
            Sembrada(almacen, Arreglo, "sanic_conversacionid", new EntityReference(Conversacion, conversacion));

            var falla = Falla(() => almacen.Delete(Conversacion, conversacion));

            AssertFalla(falla, CodigosDeFalla.RestriccionDeRelacion, "sanic_carp_conversacion_arreglo");
            Assert.True(almacen.Existe(Correo, correo) && almacen.Existe(Conversacion, conversacion));
        }

        [Fact]
        public void PA_MED_REL_08_BorrarUnaHijaNuncaEstaRestringido_YUnPadreSinHijasSeBorra_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO (CL3_H1_01 y CL3_H1_02): la relación restringe al padre, no a la hija; y una hija de OTRO padre no cuenta.
            var almacen = new OrganizationServiceEnMemoria();
            var cliente = Sembrada(almacen, Cliente);
            var otro = Sembrada(almacen, Cliente);
            var conversacion = Sembrada(almacen, Conversacion, "sanic_clienteid", new EntityReference(Cliente, otro));

            almacen.Delete(Cliente, cliente);
            almacen.Delete(Conversacion, conversacion);
            almacen.Delete(Cliente, otro);

            Assert.False(almacen.Existe(Cliente, otro));
        }

        [Fact]
        public void PA_MED_REL_09_ElDeleteRestrictCuentaLaLlamadaParaFallarEn_YNoRegistraEscrituras()
        {
            // Un Delete que falla por Restrict no es una escritura aplicada (Escrituras) y la falla pedida con FallarEn sigue teniendo prioridad sobre la relación (ordinal por tabla).
            var almacen = new OrganizationServiceEnMemoria();
            var cliente = Sembrada(almacen, Cliente);
            Sembrada(almacen, Conversacion, "sanic_clienteid", new EntityReference(Cliente, cliente));
            almacen.FallarEn(OperacionDeDatos.Delete, Cliente, 1, 4242);

            var pedida = Falla(() => almacen.Delete(Cliente, cliente));
            var restrict = Falla(() => almacen.Delete(Cliente, cliente));

            Assert.Equal(4242, pedida.Detail.ErrorCode);
            AssertFalla(restrict, CodigosDeFalla.RestriccionDeRelacion);
            Assert.Empty(almacen.Escrituras);
        }
    }
}
