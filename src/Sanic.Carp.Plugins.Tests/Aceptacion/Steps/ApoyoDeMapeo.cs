using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// Apoyos de las pruebas de los steps de Mapeo de celda (sublote 5e, S-28 a S-30): el escenario con los steps que correspondan, las versiones padre y los mapeos de partida (sembrados SIN pasar por el
    /// pipeline), la lectura de las consultas por tabla y la ejecución directa de un step con la pre-imagen que el almacén daría (o armada a mano). Vive en <c>Aceptacion/</c> para que el constructor no
    /// pueda tocarlo. Es del Mapeo: el apoyo de la Versión (<c>ApoyoDeVersion</c>) es de otro archivo.
    /// </summary>
    internal static class ApoyoDeMapeo
    {
        public const string TablaMapeo = "sanic_carp_tbl_mapeocelda";
        public const string TablaVersion = ApoyoDeArreglo.TablaVersion;
        public const string HojaReal = " FORMATO ARP ELECTRONICO";

        /// <summary>El escenario con los steps de Mapeo que se pidan. El propietario (S-06, S-13) solo si la prueba lo necesita; S-28 a S-30 se registran tal como los declara la clase.</summary>
        public static EscenarioDeSteps Escenario(bool propietario = false, bool crear = false, bool actualizar = false, bool eliminar = false)
        {
            var e = new EscenarioDeSteps();
            if (propietario)
            {
                e.Registrar(new PropietarioAlCrearStep());
                e.Registrar(new PropietarioAlActualizarStep());
            }
            if (crear) e.Registrar(new MapeoCeldaCrearStep());
            if (actualizar) e.Registrar(new MapeoCeldaActualizarStep());
            if (eliminar) e.Registrar(new MapeoCeldaEliminarStep());
            return e;
        }

        public static EscenarioDeSteps SoloCrear()
        {
            return Escenario(crear: true);
        }

        public static EscenarioDeSteps SoloActualizar()
        {
            return Escenario(actualizar: true);
        }

        public static EscenarioDeSteps SoloEliminar()
        {
            return Escenario(eliminar: true);
        }

        /// <summary>Una versión padre sembrada en ese estado (el lookup tiene que apuntar a una fila que existe).</summary>
        public static Guid UnaVersion(EscenarioDeSteps e, EstadoDeVersionDePlantilla estado = EstadoDeVersionDePlantilla.Borrador)
        {
            return e.Sembrar(ConstructorDeDatos.UnaVersion(estado));
        }

        public static EntityReference RefVersion(Guid version)
        {
            return new EntityReference(TablaVersion, version);
        }

        public static OptionSetValue Tipo(TipoDeMapeo tipo)
        {
            return new OptionSetValue((int)tipo);
        }

        public static OptionSetValue Campo(CampoMapeable campo)
        {
            return new OptionSetValue((int)campo);
        }

        public static OptionSetValue Activo()
        {
            return new OptionSetValue(Estados.Activo);
        }

        public static OptionSetValue Inactivo()
        {
            return new OptionSetValue(Estados.Inactivo);
        }

        public static OptionSetValue MotivoDe(int estado)
        {
            return new OptionSetValue(Estados.StatusPorDefecto(estado));
        }

        /// <summary>
        /// La fila de partida de un mapeo activo de <paramref name="version"/>: la hoja real, la celda <c>B7</c> y su nombre calculado, valor simple, fecha de formalización; sin filas máximas (la columna
        /// queda AUSENTE, como la devuelve Dev). Es la del constructor.
        /// </summary>
        public static ConstructorDeFila UnMapeoDe(Guid version)
        {
            return ConstructorDeDatos.UnMapeo().Con("sanic_versionplantillaid", RefVersion(version));
        }

        /// <summary>La fila de partida de la columna de bloque de la primera cuota (<c>B20</c>), 60 filas, con su nombre calculado.</summary>
        public static ConstructorDeFila UnBloqueDe(Guid version)
        {
            return UnMapeoDe(version).Con("sanic_celda", "B20").Con("sanic_nombre", HojaReal + "!B20")
                .Con("sanic_tipo", Tipo(TipoDeMapeo.ColumnaDeBloqueDeCuotas)).Con("sanic_campo", Campo(CampoMapeable.CuotaNumero)).Con("sanic_filasmaximas", 60);
        }

        public static Guid SembrarMapeo(EscenarioDeSteps e, Guid version)
        {
            return e.Sembrar(UnMapeoDe(version));
        }

        /// <summary>
        /// Un alta válida para un Create (hoja real, celda <c>b7</c> en minúscula, valor simple, fecha de formalización, el padre). Se cambia lo que se quiera con <paramref name="paresNombreValor"/>;
        /// un valor <c>null</c> deja la columna en nulo. Para quitarla del todo, <see cref="ApoyoDeArreglo.SinColumna"/>.
        /// </summary>
        public static Entity UnAlta(Guid? version, params object[] paresNombreValor)
        {
            var fila = new Entity(TablaMapeo);
            fila["sanic_hoja"] = HojaReal;
            fila["sanic_celda"] = "b7";
            fila["sanic_tipo"] = Tipo(TipoDeMapeo.ValorSimple);
            fila["sanic_campo"] = Campo(CampoMapeable.FechaDeFormalizacion);
            if (version.HasValue) fila["sanic_versionplantillaid"] = RefVersion(version.Value);
            for (int i = 0; i < paresNombreValor.Length; i += 2) fila[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return fila;
        }

        public static Entity Cambio(Guid id, params object[] paresNombreValor)
        {
            var fila = new Entity(TablaMapeo, id);
            for (int i = 0; i < paresNombreValor.Length; i += 2) fila[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return fila;
        }

        public static object Valor(EscenarioDeSteps e, Guid id, string columna)
        {
            return EscenarioDeSteps.Valor(e.Fila(TablaMapeo, id), columna);
        }

        public static string Texto(EscenarioDeSteps e, Guid id, string columna)
        {
            return Assert.IsType<string>(Valor(e, id, columna));
        }

        public static IReadOnlyList<ConsultaRegistrada> ConsultasDeLaVersion(EscenarioDeSteps e)
        {
            return e.Almacen.Consultas.Where(c => c.Tabla == TablaVersion).ToList();
        }

        public static IReadOnlyList<EjecucionDeStep> EjecucionesDe<T>(EscenarioDeSteps e)
        {
            return e.Pipeline.Ejecuciones.Where(x => x.Step.Plugin is T).ToList();
        }

        public static string[] Ordenadas(IEnumerable<string> columnas)
        {
            return columnas.OrderBy(c => c, StringComparer.Ordinal).ToArray();
        }

        /// <summary>Las columnas que un step agregó al <c>Target</c>: lo escrito menos lo que mandó el llamador (y menos el id primario, que agrega la plataforma).</summary>
        public static IReadOnlyList<string> Agregadas(EscrituraRegistrada escritura)
        {
            var idPrimario = FormaDeLaPlataforma.ColumnaDeId(TablaMapeo);
            return escritura.Columnas
                .Where(c => !escritura.ColumnasEnviadas.Contains(c, StringComparer.Ordinal) && !string.Equals(c, idPrimario, StringComparison.Ordinal))
                .ToList();
        }

        public static IReadOnlyList<EscrituraRegistrada> Escrituras(EscenarioDeSteps e, OperacionDeDatos operacion)
        {
            return ApoyoDeSteps.EscriturasDe(e.Almacen, TablaMapeo, operacion);
        }

        /// <summary>"Ninguna consulta de identidad": el primer <c>WhoAmI</c> hace fallar el guardado (si el step lo pide, sale como <c>CARP-MAP-900</c>).</summary>
        public static void SinConsultarLaIdentidad(EscenarioDeSteps e)
        {
            ApoyoDeConversacion.SinConsultarLaIdentidad(e);
        }

        /// <summary>Ejecuta un step de Update directamente (sin pipeline) con la pre-imagen <c>PreImagen</c> dada; el step modifica el <paramref name="target"/> en el lugar.</summary>
        public static void EjecutarDirecto(EscenarioDeSteps e, BaseDeStep paso, string mensaje, Guid id, Guid quien, Entity target, Entity preImagen, Action<ContextoDePluginSimulado> configurar = null)
        {
            var contexto = ApoyoDeSteps.Contexto(mensaje, 20, TablaMapeo, quien, target);
            contexto.PrimaryEntityId = id;
            if (target == null) contexto.InputParameters["Target"] = new EntityReference(TablaMapeo, id);
            if (preImagen != null) contexto.PreEntityImages["PreImagen"] = preImagen;
            if (configurar != null) configurar(contexto);
            ApoyoDeSteps.Ejecutar(paso, e.Fabrica, contexto);
        }

        public static IEnumerable<object[]> VersionesQueNoSonBorrador()
        {
            yield return new object[] { EstadoDeVersionDePlantilla.Vigente };
            yield return new object[] { EstadoDeVersionDePlantilla.Historica };
        }
    }
}
