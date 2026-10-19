using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Arreglo;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// Apoyos de las pruebas de los steps del Arreglo (sublote 5c): el escenario con los steps que correspondan, las filas de partida (con las filas que referencian YA sembradas: la plataforma valida que un lookup
    /// apunte a una fila que existe), el "cable de alarma" de la identidad (ver <see cref="ApoyoDeConversacion"/>), la lectura de las cuotas y la ejecución directa de un step con la pre-imagen que el almacén daría.
    /// Vive en <c>Aceptacion/</c> para que el constructor no pueda tocarlo.
    /// </summary>
    internal static class ApoyoDeArreglo
    {
        public const string Arr = "sanic_carp_tbl_arreglo";
        public const string Cuota = "sanic_carp_tbl_cuota";
        public const string TablaVersion = "sanic_carp_tbl_versionplantilla";
        public const string TablaCliente = "sanic_carp_tbl_cliente";
        public const string Conversacion = "sanic_carp_tbl_conversacion";
        public const string Usuario = "systemuser";

        public static EscenarioDeSteps Escenario(bool propietario = true, bool crear = true, bool calendario = true, bool actualizar = true)
        {
            var e = new EscenarioDeSteps();
            if (propietario)
            {
                e.Registrar(new PropietarioAlCrearStep());
                e.Registrar(new PropietarioAlActualizarStep());
            }
            if (crear) e.Registrar(new ArregloCrearStep());
            if (calendario) e.Registrar(new ArregloCalendarioStep());
            if (actualizar) e.Registrar(new ArregloActualizarStep());
            return e;
        }

        public static OptionSetValue State(EstadoDeArreglo estado)
        {
            return new OptionSetValue(Estados.De(estado).StateCode);
        }

        public static OptionSetValue Status(EstadoDeArreglo estado)
        {
            return new OptionSetValue(Estados.De(estado).StatusCode);
        }

        public static DateTime Fecha(int anio, int mes, int dia)
        {
            return new DateTime(anio, mes, dia, 0, 0, 0, DateTimeKind.Unspecified);
        }

        public static EntityReference RefUsuario(Guid id)
        {
            return new EntityReference(Usuario, id);
        }

        /// <summary>Una versión Vigente sembrada con esa capacidad.</summary>
        public static Guid UnaVersionVigente(EscenarioDeSteps e, int capacidad = 60)
        {
            return e.Sembrar(ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Vigente).Con("sanic_capacidad", capacidad));
        }

        public static Guid UnCliente(EscenarioDeSteps e)
        {
            return e.Sembrar(ConstructorDeDatos.UnCliente());
        }

        public static Guid UnaConversacion(EscenarioDeSteps e)
        {
            return e.Sembrar(ConstructorDeDatos.UnaConversacion());
        }

        /// <summary>
        /// Un alta válida (inicial 1000, abonado 0, cuota 300, plazo 4, inicio 2026-11-05, número de producto con espacios) para un Create. Sin lookups: los que se necesiten se agregan con <paramref name="paresNombreValor"/>
        /// (y se siembran antes).
        /// </summary>
        public static Entity UnAlta(params object[] paresNombreValor)
        {
            var fila = new Entity(Arr);
            fila["sanic_producto"] = new OptionSetValue((int)ProductoCrediticio.Prestamo);
            fila["sanic_numeroproducto"] = " 5412 7500 ";
            fila["sanic_saldoinicial"] = 1000m;
            fila["sanic_montoabonado"] = 0m;
            fila["sanic_cuotamensual"] = 300m;
            fila["sanic_plazo"] = 4;
            fila["sanic_fechainicio"] = Fecha(2026, 11, 5);
            for (int i = 0; i < paresNombreValor.Length; i += 2) fila[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return fila;
        }

        public static Entity SinColumna(Entity fila, params string[] columnas)
        {
            foreach (var columna in columnas) fila.Attributes.Remove(columna);
            return fila;
        }

        /// <summary>
        /// La fila de partida de un arreglo en <paramref name="estado"/> (la del constructor: inicial 1000,50, abonado 100,50, cuota 150, plazo 6, inicio 2026-11-01, fin 2027-04-01, remanente 900), con el cliente, la versión y el
        /// oficial SEMBRADOS. Un <paramref name="congelado"/> trae el congelamiento, las cuatro instantáneas y el provisional 3.
        /// </summary>
        public static ConstructorDeFila Fila(EscenarioDeSteps e, EstadoDeArreglo estado, bool congelado = false, Guid? version = null)
        {
            var fila = ConstructorDeDatos.UnArreglo(estado)
                .Con("sanic_clienteid", new EntityReference(TablaCliente, UnCliente(e)))
                .Con("sanic_versionplantillaid", new EntityReference(TablaVersion, version ?? UnaVersionVigente(e)))
                .Con("sanic_oficialid", RefUsuario(e.Oficial));
            if (congelado)
            {
                fila.Con("sanic_fechacongelamiento", DateTime.UtcNow.AddDays(-1))
                    .Con("sanic_instantaneanombre", "ZZF Cliente Uno").Con("sanic_instantaneacedula", "001-000000-0001A")
                    .Con("sanic_instantaneacorreo", "uno@example.com").Con("sanic_instantaneacelular", "88880000")
                    .Con("sanic_numeroprovisional", 3);
            }
            return fila;
        }

        /// <summary>Siembra las cuotas 1..N del arreglo (vencimientos mensuales desde <paramref name="inicio"/>, cada una con <paramref name="monto"/>). Devuelve sus ids en orden.</summary>
        public static IReadOnlyList<Guid> SembrarCuotas(EscenarioDeSteps e, Guid arreglo, int cuantas, DateTime inicio, decimal monto, string nombreDelArreglo = "ARR-000123")
        {
            var ids = new List<Guid>();
            for (int n = 1; n <= cuantas; n++)
            {
                ids.Add(e.Sembrar(ConstructorDeDatos.UnaCuota()
                    .Con("sanic_arregloid", new EntityReference(Arr, arreglo))
                    .Con("sanic_numero", n)
                    .Con("sanic_nombre", nombreDelArreglo + "-C" + n.ToString("D2", System.Globalization.CultureInfo.InvariantCulture))
                    .Con("sanic_fechavencimiento", inicio.AddMonths(n - 1))
                    .Con("sanic_monto", monto)));
            }
            return ids;
        }

        /// <summary>Cambia columnas de una cuota sembrada con el servicio de SYSTEM (la partida de la prueba: el llamador de la prueba mide después y limpia los registros).</summary>
        public static void Modificar(EscenarioDeSteps e, Guid cuota, params object[] paresNombreValor)
        {
            var cambio = new Entity(Cuota, cuota);
            for (int i = 0; i < paresNombreValor.Length; i += 2) cambio[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            e.ComoSistema.Update(cambio);
        }

        /// <summary>Las cuotas del arreglo que hay en el almacén, por número.</summary>
        public static IReadOnlyList<Entity> CuotasDe(EscenarioDeSteps e, Guid arreglo)
        {
            return e.Almacen.Todas(Cuota)
                .Where(c => c.Contains("sanic_arregloid") && ((EntityReference)c["sanic_arregloid"]).Id == arreglo)
                .OrderBy(c => (int)c["sanic_numero"])
                .ToList();
        }

        public static Entity Cambio(Guid id, params object[] paresNombreValor)
        {
            var fila = new Entity(Arr, id);
            for (int i = 0; i < paresNombreValor.Length; i += 2) fila[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return fila;
        }

        public static Entity Con(Entity fila, params object[] paresNombreValor)
        {
            for (int i = 0; i < paresNombreValor.Length; i += 2) fila[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return fila;
        }

        public static object Valor(EscenarioDeSteps e, Guid id, string columna)
        {
            return EscenarioDeSteps.Valor(e.Fila(Arr, id), columna);
        }

        public static EscrituraRegistrada UnicaActualizacion(EscenarioDeSteps e)
        {
            return Assert.Single(ApoyoDeSteps.EscriturasDe(e.Almacen, Arr, OperacionDeDatos.Update));
        }

        /// <summary>Las consultas (Retrieve y RetrieveMultiple) sobre una tabla.</summary>
        public static IReadOnlyList<ConsultaRegistrada> ConsultasDe(EscenarioDeSteps e, string tabla)
        {
            return e.Almacen.Consultas.Where(c => c.Tabla == tabla).ToList();
        }

        /// <summary>Las consultas de pagos (<c>HayPagos</c>): las que filtran la cuota por <c>sanic_fechapago</c>.</summary>
        public static int ConsultasDePagos(EscenarioDeSteps e)
        {
            return e.Almacen.Consultas.Count(c => c.Tabla == Cuota && c.Condiciones.Any(x => x.Columna == "sanic_fechapago"));
        }

        public static IReadOnlyList<string> AgregadasPorLosSteps(EscrituraRegistrada escritura)
        {
            return escritura.Columnas.Where(c => !escritura.ColumnasEnviadas.Contains(c, StringComparer.Ordinal) && !string.Equals(c, "sanic_nombre", StringComparison.Ordinal)).ToList();
        }

        public static string[] Ordenadas(IEnumerable<string> columnas)
        {
            return columnas.OrderBy(c => c, StringComparer.Ordinal).ToArray();
        }

        /// <summary>Ejecuta el step directamente (sin pipeline) con la pre-imagen <c>PreImagen</c> que el almacén daría a <paramref name="quien"/>; el step modifica el <paramref name="target"/> en el lugar.</summary>
        public static void EjecutarDirecto(EscenarioDeSteps e, BaseDeStep paso, Guid id, Guid quien, Entity target, Action<ContextoDePluginSimulado> configurar = null)
        {
            var contexto = ApoyoDeSteps.Contexto("Update", 20, Arr, quien, target);
            contexto.PrimaryEntityId = id;
            contexto.PreEntityImages["PreImagen"] = e.Almacen.ImagenPrevia(Arr, id, ReglasDeArreglo.ColumnasDeLaPreImagenDeActualizacion, quien);
            if (configurar != null) configurar(contexto);
            ApoyoDeSteps.Ejecutar(paso, e.Fabrica, contexto);
        }

        /// <summary>Un valor DISTINTO del de la fila de partida, válido para el esquema, de cada dato del acuerdo (el almacén valida tipo, largo y que el lookup exista).</summary>
        public static object ValorDistinto(EscenarioDeSteps e, string columna)
        {
            switch (columna)
            {
                case "sanic_producto": return new OptionSetValue((int)ProductoCrediticio.Tarjeta);
                case "sanic_tipo": return new OptionSetValue((int)TipoDeArreglo.Cancelacion);
                case "sanic_moneda": return new OptionSetValue((int)Moneda.Dolares);
                case "sanic_canalorigen": return new OptionSetValue((int)CanalDeOrigen.Presencial);
                case "sanic_saldoinicial": return 2000m;
                case "sanic_montoabonado": return 10m;
                case "sanic_plazo": return 3;
                case "sanic_cuotamensual": return 350m;
                case "sanic_fechainicio": return Fecha(2026, 12, 1);
                case "sanic_fechafin": return Fecha(2027, 6, 1);
                case "sanic_fechaformalizacion": return Fecha(2026, 9, 9);
                case "sanic_clienteid": return new EntityReference(TablaCliente, UnCliente(e));
                case "sanic_oficialid": return RefUsuario(e.OtroOficial);
                case "sanic_numeroproducto": return "ZZF-OTRO-NUMERO";
                case "sanic_cuentadebitar": return "ZZF-otra-cuenta";
                case "sanic_comentarios": return "ZZF otro comentario";
                default: throw new ArgumentOutOfRangeException(nameof(columna), columna, "Columna sin valor distinto en el apoyo.");
            }
        }

        /// <summary>Un valor válido para el esquema de cada columna solo-sistema (menos <c>sanic_nombre</c>), para un alta de una persona.</summary>
        public static object ValorSoloSistema(EscenarioDeSteps e, string columna, Guid version)
        {
            switch (columna)
            {
                case "sanic_numeroarreglo":
                case "sanic_numeroprovisional":
                case "sanic_cantidadreaperturas": return 2;
                case "sanic_saldoremanente":
                case "sanic_totalpagar": return 10m;
                case "sanic_fechacongelamiento":
                case "sanic_fechaformalizado":
                case "sanic_fechadescarte":
                case "sanic_fechaanulacion":
                case "sanic_fechareapertura":
                case "sanic_fechatermino": return DateTime.UtcNow.AddDays(-2);
                case "sanic_formalizadoporid":
                case "sanic_descartadoporid":
                case "sanic_anuladoporid":
                case "sanic_reabiertoporid": return RefUsuario(e.OtroOficial);
                case "sanic_versionplantillaid": return new EntityReference(TablaVersion, version);
                case "sanic_instantaneacelular": return "88880000";
                default: return "ZZF-" + columna.Substring(Math.Max(0, columna.Length - 8));
            }
        }

        public static IEnumerable<object[]> ColumnasSoloSistemaSinElNombre()
        {
            foreach (var columna in Columnas.De(Columnas.TablaArreglo).SoloSistema.Where(c => c != "sanic_nombre")) yield return new object[] { columna };
        }

        public static IEnumerable<object[]> DatosDelAcuerdo()
        {
            foreach (var columna in Columnas.De(Columnas.TablaArreglo).DatosDelAcuerdo) yield return new object[] { columna };
        }
    }
}
