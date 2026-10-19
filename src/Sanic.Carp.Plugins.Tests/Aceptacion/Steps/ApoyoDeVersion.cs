using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Aceptacion.Formato;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// Apoyos de las pruebas de los steps de Versión de plantilla (sublote 5e, vuelta 2A): el escenario con los steps que correspondan (se apoya en <see cref="ApoyoDeArreglo"/>), las versiones de partida
    /// (con el archivo sembrado con <c>SembrarArchivo</c>: el doble NO deja escribir <c>sanic_archivo</c> por <c>Create</c> ni <c>Update</c>), el mapeo v07 de 27 filas, la lectura del almacén y la ejecución
    /// directa de un step con la pre-imagen que el almacén daría. Vive en <c>Aceptacion/</c> para que el constructor no pueda tocarlo.
    /// <para>
    /// <b>La hora.</b> Los steps leen <c>contexto.Reloj</c>, el reloj de producción: una prueba de pipeline no puede fijar "ahora". Las fechas de aprobación se afirman contra una ventana
    /// [antes, después] tomada con <c>DateTime.UtcNow</c> alrededor de la llamada.
    /// </para>
    /// </summary>
    internal static class ApoyoDeVersion
    {
        public const string TablaVersion = ApoyoDeArreglo.TablaVersion;
        public const string TablaMapeo = "sanic_carp_tbl_mapeocelda";
        public const string HojaFormato = " FORMATO ARP ELECTRONICO";   // con el espacio inicial: spike 1, hallazgo 1
        public const string HojaControl = "CONTROL DE PAGOS";

        /// <summary>La huella de partida de las versiones que ya pasaron por Publicar (texto fijo: lo que se compara es que cambie o no).</summary>
        public static readonly string HuellaDePartida = Plantillas.Sha256(Encoding.UTF8.GetBytes("huella de partida de las pruebas de version"));

        /// <summary>El escenario con el área sembrada y los steps de Versión que se pidan registrados (el propietario, S-05 a S-14, solo si se pide).</summary>
        public static EscenarioDeSteps Escenario(bool propietario = false, bool crear = false, bool actualizar = false, bool archivo = false)
        {
            var e = ApoyoDeArreglo.Escenario(propietario: propietario, crear: false, calendario: false, actualizar: false);
            if (crear) e.Registrar(new VersionPlantillaCrearStep());
            if (actualizar) e.Registrar(new VersionPlantillaActualizarStep());
            if (archivo) e.Registrar(new ArchivoDePlantillaStep());
            return e;
        }

        public static OptionSetValue Opcion(EstadoDeVersionDePlantilla estado)
        {
            return new OptionSetValue((int)estado);
        }

        /// <summary>
        /// Una versión sembrada en <paramref name="estado"/> (sin pasar por el pipeline). Las que no son Borrador traen huella y un aprobador que EXISTE (el doble valida que un lookup apunte a una fila real).
        /// Con <paramref name="archivo"/> la fila trae <c>sanic_archivo</c> y el contenido queda sembrado.
        /// </summary>
        public static Guid UnaVersion(EscenarioDeSteps e, EstadoDeVersionDePlantilla estado = EstadoDeVersionDePlantilla.Borrador, string nombre = "v08", int capacidad = 60,
            byte[] archivo = null, bool inactiva = false)
        {
            var fila = ConstructorDeDatos.UnaVersion(estado).Con("sanic_nombre", nombre).Con("sanic_capacidad", capacidad);
            if (estado != EstadoDeVersionDePlantilla.Borrador)
                fila.Con("sanic_huella", HuellaDePartida).Con("sanic_aprobadoporid", new EntityReference(ApoyoDeArreglo.Usuario, e.OtroOficial));
            if (inactiva)
                fila.Con("statecode", new OptionSetValue(Estados.Inactivo)).Con("statuscode", new OptionSetValue(Estados.StatusPorDefecto(Estados.Inactivo)));
            if (archivo != null) fila.Con("sanic_archivo", Guid.NewGuid());
            var id = e.Sembrar(fila);
            if (archivo != null) e.Almacen.SembrarArchivo(TablaVersion, id, "sanic_archivo", "plantilla.xlsx", archivo);
            return id;
        }

        /// <summary>Un Borrador con la plantilla v07 y el mapeo v07 de 27 filas (capacidad 60): lo que Publicar acepta.</summary>
        public static Guid UnBorradorPublicable(EscenarioDeSteps e, string nombre = "v08", int capacidad = 60)
        {
            var id = UnaVersion(e, EstadoDeVersionDePlantilla.Borrador, nombre, capacidad, Plantillas.V07);
            SembrarMapeo(e, id, MapeoV07());
            return id;
        }

        /// <summary>Siembra una fila de <c>sanic_carp_tbl_mapeocelda</c> por cada fila del dominio (sin pasar por el pipeline). Un campo nulo en la fila del dominio queda AUSENTE en la tabla.</summary>
        public static IReadOnlyList<Guid> SembrarMapeo(EscenarioDeSteps e, Guid version, IEnumerable<FilaDeMapeo> filas)
        {
            var ids = new List<Guid>();
            foreach (var fila in filas)
            {
                var constructor = ConstructorDeDatos.UnMapeo()
                    .Con("sanic_versionplantillaid", new EntityReference(TablaVersion, version))
                    .Con("sanic_hoja", fila.Hoja)
                    .Con("sanic_celda", fila.Celda)
                    .Con("sanic_nombre", (fila.Hoja ?? string.Empty) + "!" + (fila.Celda ?? string.Empty))
                    .Con("sanic_tipo", new OptionSetValue((int)fila.Tipo))
                    .Con("sanic_campo", new OptionSetValue((int)fila.Campo))
                    .Con("sanic_filasmaximas", fila.FilasMaximas);
                ids.Add(e.Sembrar(constructor));
            }
            return ids;
        }

        private static FilaDeMapeo Simple(string hoja, string celda, CampoMapeable campo)
        {
            return new FilaDeMapeo(hoja, celda, TipoDeMapeo.ValorSimple, campo);
        }

        private static FilaDeMapeo Bloque(string hoja, string celda, CampoMapeable campo, int filas)
        {
            return new FilaDeMapeo(hoja, celda, TipoDeMapeo.ColumnaDeBloqueDeCuotas, campo, filas);
        }

        private static FilaDeMapeo Total(string hoja, string celda, CampoMapeable campo)
        {
            return new FilaDeMapeo(hoja, celda, TipoDeMapeo.TotalCalculado, campo);
        }

        /// <summary>
        /// El mapeo de la v07: las 27 opciones de <see cref="CampoMapeable"/>, una fila cada una (copia del de <c>ValidadorDePublicacionAceptacion</c>, que es privado). El bloque ocupa A7:E66
        /// (capacidad 60) y los totales la fila 67. Cada llamada devuelve una lista nueva.
        /// </summary>
        public static List<FilaDeMapeo> MapeoV07(int filasDelBloque = 60)
        {
            return new List<FilaDeMapeo>
            {
                Simple(HojaFormato, "B7", CampoMapeable.FechaDeFormalizacion), Simple(HojaFormato, "F7", CampoMapeable.OficialDeArreglo),
                Simple(HojaFormato, "B8", CampoMapeable.NombreDelCliente), Simple(HojaFormato, "F8", CampoMapeable.CedulaDelCliente),
                Simple(HojaFormato, "B9", CampoMapeable.CorreoDelCliente), Simple(HojaFormato, "F9", CampoMapeable.CelularDelCliente),
                Simple(HojaFormato, "B12", CampoMapeable.NumeroDePrestamoOTarjeta), Simple(HojaFormato, "D12", CampoMapeable.TipoDeArreglo),
                Simple(HojaFormato, "F12", CampoMapeable.NumeroDeArreglo),
                Simple(HojaFormato, "B13", CampoMapeable.SaldoInicial), Simple(HojaFormato, "D13", CampoMapeable.MontoAbonado),
                Simple(HojaFormato, "F13", CampoMapeable.SaldoRemanente),
                Simple(HojaFormato, "B14", CampoMapeable.PlazoEnCuotas), Simple(HojaFormato, "D14", CampoMapeable.CuotaMensual),
                Simple(HojaFormato, "F14", CampoMapeable.TotalAPagar),
                Simple(HojaFormato, "B15", CampoMapeable.FechaDeInicio), Simple(HojaFormato, "D15", CampoMapeable.FechaDeFinalizacion),
                Simple(HojaFormato, "F15", CampoMapeable.CuentaADebitar),
                Simple(HojaFormato, "B16", CampoMapeable.Moneda),
                Simple(HojaFormato, "A18", CampoMapeable.Comentarios),
                Bloque(HojaControl, "A7", CampoMapeable.CuotaNumero, filasDelBloque),
                Bloque(HojaControl, "B7", CampoMapeable.CuotaFechaDeVencimiento, filasDelBloque),
                Bloque(HojaControl, "C7", CampoMapeable.CuotaMonto, filasDelBloque),
                Bloque(HojaControl, "D7", CampoMapeable.CuotaFechaDePago, filasDelBloque),
                Bloque(HojaControl, "E7", CampoMapeable.CuotaMontoPagado, filasDelBloque),
                Total(HojaControl, "C67", CampoMapeable.TotalMontosDeCuota),
                Total(HojaControl, "E67", CampoMapeable.TotalMontosPagados)
            };
        }

        /// <summary>Un cambio de la versión: el id y los pares nombre-valor.</summary>
        public static Entity Cambio(Guid id, params object[] paresNombreValor)
        {
            var fila = new Entity(TablaVersion, id);
            for (int i = 0; i < paresNombreValor.Length; i += 2) fila[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return fila;
        }

        /// <summary>El gesto Publicar: solo <c>sanic_estado</c> = Vigente.</summary>
        public static Entity Publicar(Guid id)
        {
            return Cambio(id, "sanic_estado", Opcion(EstadoDeVersionDePlantilla.Vigente));
        }

        public static object Valor(EscenarioDeSteps e, Guid id, string columna)
        {
            return EscenarioDeSteps.Valor(e.Fila(TablaVersion, id), columna);
        }

        /// <summary>El <c>sanic_estado</c> guardado de la versión (el entero de la opción), o <c>null</c> si falta.</summary>
        public static int? Estado(EscenarioDeSteps e, Guid id)
        {
            var valor = Valor(e, id, "sanic_estado") as OptionSetValue;
            return valor == null ? (int?)null : valor.Value;
        }

        /// <summary>Los ids de las versiones Vigentes y activas (el filtro de <c>OtrasVigentes</c>).</summary>
        public static IReadOnlyList<Guid> VigentesActivas(EscenarioDeSteps e)
        {
            return e.Almacen.Todas(TablaVersion)
                .Where(f => f.Contains("sanic_estado") && ((OptionSetValue)f["sanic_estado"]).Value == (int)EstadoDeVersionDePlantilla.Vigente
                    && (!f.Contains("statecode") || ((OptionSetValue)f["statecode"]).Value == Estados.Activo))
                .Select(f => f.Id)
                .ToList();
        }

        public static IReadOnlyList<ConsultaRegistrada> ConsultasDe(EscenarioDeSteps e, string tabla)
        {
            return e.Almacen.Consultas.Where(c => c.Tabla == tabla).ToList();
        }

        public static IReadOnlyList<EjecucionDeStep> EjecucionesDe<T>(EscenarioDeSteps e)
        {
            return e.Pipeline.Ejecuciones.Where(x => x.Step.Plugin is T).ToList();
        }

        public static IReadOnlyList<EscrituraRegistrada> Escrituras(EscenarioDeSteps e, string tabla)
        {
            return e.Almacen.Escrituras.Where(x => x.Tabla == tabla).ToList();
        }

        /// <summary>El rechazo es una <see cref="InvalidPluginExecutionException"/> con EXACTAMENTE el texto <c>CARP-XXX-NNN: …</c> del error dado (para los mensajes que arma el dominio, no el catálogo).</summary>
        public static InvalidPluginExecutionException RechazaConTexto(Action accion, string codigo, string mensaje)
        {
            var rechazo = Assert.Throws<InvalidPluginExecutionException>(accion);
            Assert.Equal(new ErrorDeNegocio(codigo, mensaje).ToString(), rechazo.Message);
            return rechazo;
        }

        /// <summary>El contexto de una ejecución directa de S-26 con la pre-imagen <c>PreImagen</c> que el almacén daría a <paramref name="quien"/>.</summary>
        public static ContextoDePluginSimulado ContextoDeActualizacion(EscenarioDeSteps e, Guid id, Guid quien, Entity target, bool conPreImagen = true)
        {
            var contexto = ApoyoDeSteps.Contexto("Update", 20, TablaVersion, quien, target);
            contexto.PrimaryEntityId = id;
            if (conPreImagen) contexto.PreEntityImages["PreImagen"] = e.Almacen.ImagenPrevia(TablaVersion, id, ReglasDeVersion.ColumnasDeLaPreImagen, quien);
            return contexto;
        }

        /// <summary>Ejecuta el step directamente (sin pipeline) con la pre-imagen de la versión; el step modifica el <paramref name="target"/> en el lugar. <paramref name="configurar"/> cambia el contexto.</summary>
        public static void EjecutarDirecto(EscenarioDeSteps e, BaseDeStep paso, Guid id, Guid quien, Entity target, Action<ContextoDePluginSimulado> configurar = null)
        {
            var contexto = ContextoDeActualizacion(e, id, quien, target);
            if (configurar != null) configurar(contexto);
            ApoyoDeSteps.Ejecutar(paso, e.Fabrica, contexto);
        }

        /// <summary>
        /// Ejecuta <see cref="ArchivoDePlantillaStep"/> directamente (S-27): el <c>Target</c> trae el id de la versión y <c>sanic_archivo</c> (un <see cref="Guid"/> o <c>null</c>); no lleva pre-imagen.
        /// <paramref name="configurar"/> cambia el contexto (<c>UserId</c>, <c>Depth</c>, el mensaje padre…).
        /// </summary>
        public static void EjecutarArchivo(EscenarioDeSteps e, Guid id, Guid quien, object archivo, Action<ContextoDePluginSimulado> configurar = null)
        {
            var target = new Entity(TablaVersion, id);
            target["sanic_archivo"] = archivo;
            var contexto = ApoyoDeSteps.Contexto("Update", 20, TablaVersion, quien, target);
            contexto.PrimaryEntityId = id;
            if (configurar != null) configurar(contexto);
            ApoyoDeSteps.Ejecutar(new ArchivoDePlantillaStep(), e.Fabrica, contexto);
        }

        public static string[] Ordenadas(IEnumerable<string> columnas)
        {
            return columnas.OrderBy(c => c, StringComparer.Ordinal).ToArray();
        }

        /// <summary>Las columnas que un step agregó al <c>Target</c>: lo escrito menos lo que mandó el llamador (y menos <c>sanic_nombre</c>, que no es de estos steps).</summary>
        public static IReadOnlyList<string> AgregadasPorLosSteps(EscrituraRegistrada escritura)
        {
            return escritura.Columnas.Where(c => !escritura.ColumnasEnviadas.Contains(c, StringComparer.Ordinal)).ToList();
        }
    }
}
