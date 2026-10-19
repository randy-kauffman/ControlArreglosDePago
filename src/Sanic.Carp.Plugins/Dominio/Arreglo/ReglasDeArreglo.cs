using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Sanic.Carp.Plugins.Dominio.Calendario;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Montos;

namespace Sanic.Carp.Plugins.Dominio.Arreglo
{
    /// <summary>El calendario de cuotas que el arreglo necesita (<see cref="ReglasDeArreglo.PlanificarCalendario"/>), o el error que impide armarlo.</summary>
    public sealed class PlanDeCalendario
    {
        /// <summary><c>true</c>: <see cref="Cuotas"/> tiene las <c>plazo</c> cuotas. <c>false</c>: <see cref="Error"/> dice por qué.</summary>
        public bool Ok { get; }

        /// <summary><c>CARP-ARR-006</c>, <c>-004</c>, <c>-005</c> o <c>-007</c>; <c>null</c> si <see cref="Ok"/>.</summary>
        public ErrorDeNegocio Error { get; }

        /// <summary>Las cuotas 1..plazo (<see cref="GeneradorDeCalendario.Generar"/>); vacía si no <see cref="Ok"/>. Solo lectura de verdad.</summary>
        public IReadOnlyList<CuotaDeCalendario> Cuotas { get; }

        private PlanDeCalendario(bool ok, ErrorDeNegocio error, IReadOnlyList<CuotaDeCalendario> cuotas)
        {
            Ok = ok;
            Error = error;
            Cuotas = cuotas;
        }

        internal static PlanDeCalendario Exito(IReadOnlyList<CuotaDeCalendario> cuotas)
        {
            return new PlanDeCalendario(true, null, new ReadOnlyCollection<CuotaDeCalendario>(new List<CuotaDeCalendario>(cuotas)));
        }

        internal static PlanDeCalendario Fallo(ErrorDeNegocio error)
        {
            return new PlanDeCalendario(false, error, new ReadOnlyCollection<CuotaDeCalendario>(new List<CuotaDeCalendario>()));
        }
    }

    /// <summary>
    /// Las reglas del Arreglo (diseno/03 2.5 y 2.6, firmas de 4.4; "Decisiones del arquitecto, 2026-10-19 (sublote 5c)"). Dominio puro: sin <c>Entity</c> ni SDK, sin estado (ningún campo mutable, ni estático:
    /// las listas son <see cref="ReadOnlyCollection{T}"/>), sin leer el reloj (entra en el pedido) y con la identidad perezosa. Usa las piezas del lote 1 (<c>ReglasDeMontos</c>,
    /// <c>GeneradorDeCalendario</c>, <c>ConsecutivoDeCredito</c>, <c>NormalizadorDeTexto</c>, <c>Cambios</c>, <c>Estados</c>, <c>Columnas</c>) sin reimplementarlas.
    /// </summary>
    public static class ReglasDeArreglo
    {
        private static readonly IReadOnlyList<string> PreImagenDeActualizacion = new ReadOnlyCollection<string>(new[]
        {
            "statecode", "statuscode", "sanic_nombre", "sanic_producto", "sanic_numeroproducto", "sanic_numeroarreglo", "sanic_numeroprovisional", "sanic_tipo",
            "sanic_moneda", "sanic_canalorigen", "sanic_saldoinicial", "sanic_montoabonado", "sanic_saldoremanente", "sanic_plazo", "sanic_cuotamensual",
            "sanic_totalpagar", "sanic_fechainicio", "sanic_fechafin", "sanic_cuentadebitar", "sanic_fechaformalizacion", "sanic_comentarios",
            "sanic_fechacongelamiento", "sanic_instantaneanombre", "sanic_instantaneacedula", "sanic_instantaneacorreo", "sanic_instantaneacelular",
            "sanic_fechaformalizado", "sanic_formalizadoporid", "sanic_motivodescarte", "sanic_fechadescarte", "sanic_descartadoporid", "sanic_motivoanulacion",
            "sanic_fechaanulacion", "sanic_anuladoporid", "sanic_motivoreapertura", "sanic_fechareapertura", "sanic_reabiertoporid", "sanic_cantidadreaperturas",
            "sanic_fechatermino", "sanic_versionplantillaid", "sanic_clienteid", "sanic_conversacionid", "sanic_oficialid"
        });

        private static readonly IReadOnlyList<string> Protegidas = new ReadOnlyCollection<string>(new[] { "sanic_cuentadebitar", "sanic_instantaneacedula" });

        private const string PrefijoDelNombre = "ARR-";
        private const int MinimoDeDigitos = 6;
        private const int MaximoDeDigitos = 16;   // la columna mide 20 y `ARR-` ocupa 4 (2.5, paso 2)

        private const string ColNombre = "sanic_nombre";
        private const string ColEstado = "statecode";
        private const string ColMotivoDeEstado = "statuscode";
        private const string ColProducto = "sanic_producto";
        private const string ColNumeroProducto = "sanic_numeroproducto";
        private const string ColNumeroArreglo = "sanic_numeroarreglo";
        private const string ColNumeroProvisional = "sanic_numeroprovisional";
        private const string ColCanalDeOrigen = "sanic_canalorigen";
        private const string ColSaldoInicial = "sanic_saldoinicial";
        private const string ColMontoAbonado = "sanic_montoabonado";
        private const string ColSaldoRemanente = "sanic_saldoremanente";
        private const string ColPlazo = "sanic_plazo";
        private const string ColCuotaMensual = "sanic_cuotamensual";
        private const string ColTotalPagar = "sanic_totalpagar";
        private const string ColFechaInicio = "sanic_fechainicio";
        private const string ColFechaFin = "sanic_fechafin";
        private const string ColCongelamiento = "sanic_fechacongelamiento";
        private const string ColFechaFormalizacion = "sanic_fechaformalizacion";
        private const string ColInstantaneaNombre = "sanic_instantaneanombre";
        private const string ColInstantaneaCedula = "sanic_instantaneacedula";
        private const string ColInstantaneaCorreo = "sanic_instantaneacorreo";
        private const string ColInstantaneaCelular = "sanic_instantaneacelular";
        private const string ColFechaFormalizado = "sanic_fechaformalizado";
        private const string ColFormalizadoPor = "sanic_formalizadoporid";
        private const string ColMotivoDescarte = "sanic_motivodescarte";
        private const string ColFechaDescarte = "sanic_fechadescarte";
        private const string ColDescartadoPor = "sanic_descartadoporid";
        private const string ColMotivoAnulacion = "sanic_motivoanulacion";
        private const string ColFechaAnulacion = "sanic_fechaanulacion";
        private const string ColAnuladoPor = "sanic_anuladoporid";
        private const string ColMotivoReapertura = "sanic_motivoreapertura";
        private const string ColFechaReapertura = "sanic_fechareapertura";
        private const string ColReabiertoPor = "sanic_reabiertoporid";
        private const string ColCantidadReaperturas = "sanic_cantidadreaperturas";
        private const string ColFechaTermino = "sanic_fechatermino";
        private const string ColVersion = "sanic_versionplantillaid";
        private const string ColConversacion = "sanic_conversacionid";
        private const string TablaDeUsuarios = "systemuser";

        private enum Gesto
        {
            Formalizar,
            Descartar,
            Anular,
            ReabrirTerminado,
            ReabrirVigente,
            Desbloquear
        }

        /// <summary>
        /// Las 43 columnas de la pre-imagen de S-21 (2.6 y sección 5), en el orden de 2.6. Es la unión de las cuatro categorías de <c>Columnas.De(TablaArreglo)</c> (solo-sistema, de gesto, datos del acuerdo y
        /// libres) más <c>statecode</c> y <c>statuscode</c>; <c>ownerid</c> no va (lo rige el step de propietario, 2.1). El step declara su registro con esta lista y <b>este orden es el de los nombres de columna en los
        /// mensajes</b> <c>CARP-ARR-009</c>, <c>-011</c> y <c>-012</c>.
        /// </summary>
        public static IReadOnlyList<string> ColumnasDeLaPreImagenDeActualizacion { get { return PreImagenDeActualizacion; } }

        /// <summary>
        /// Las columnas del Arreglo con seguridad por columna (DD-06, perfil <c>CSP - CARP - Datos sensibles</c>): <c>sanic_cuentadebitar</c> y <c>sanic_instantaneacedula</c>. Un llamador sin el perfil recibe la
        /// pre-imagen SIN ellas (ausentes, no nulas; V-6) y el step las completa con SYSTEM cuando el <c>Target</c> las trae (DC-28). La lista de los pedidos de lectura del step sale de aquí.
        /// </summary>
        public static IReadOnlyList<string> ColumnasConSeguridadPorColumna { get { return Protegidas; } }

        /// <summary>
        /// ¿Cumple <paramref name="nombre"/> el formato del autonumérico del Arreglo, <c>^ARR-[0-9]{6,16}$</c> (V-8, D-40, 2.5 paso 2)? Comparación ORDINAL y sensible a mayúsculas, sin recortar espacios:
        /// <c>"ARR-001002"</c> sí; <c>null</c>, el texto vacío, <c>"arr-001002"</c>, <c>"ARR-12345"</c> (5 dígitos), 17 dígitos (no cabe en la columna de 20), un espacio al principio o al final, un salto de línea
        /// al final y cualquier carácter que no sea un dígito ASCII (uno de ancho completo o arábigo-índico) no. Nunca lanza. Hay que recorrer los caracteres a mano o con un patrón que acabe en <c>\z</c>
        /// (en .NET el <c>$</c> acepta un salto de línea final y <c>\d</c> acepta dígitos Unicode); sin <c>Regex</c> estático.
        /// </summary>
        public static bool NombreTieneFormatoDeAutonumerico(string nombre)
        {
            if (nombre == null) return false;
            // Se recorre a mano (5c, V-8): el `$` de .NET acepta un salto de linea final y `\d` acepta digitos de otros alfabetos.
            if (nombre.Length < PrefijoDelNombre.Length + MinimoDeDigitos || nombre.Length > PrefijoDelNombre.Length + MaximoDeDigitos) return false;
            if (!nombre.StartsWith(PrefijoDelNombre, StringComparison.Ordinal)) return false;
            for (int i = PrefijoDelNombre.Length; i < nombre.Length; i++)
                if (nombre[i] < '0' || nombre[i] > '9') return false;
            return true;
        }

        /// <summary>
        /// El nombre de la cuota <paramref name="numero"/> de un arreglo (2.5, calendario paso 5): <c>nombreDelArreglo + "-C" + NN</c>, con <c>NN</c> de dos dígitos y cultura invariante
        /// (<c>ARR-000123-C07</c>, <c>ARR-000123-C60</c>). <paramref name="nombreDelArreglo"/> nulo ⇒ <see cref="ArgumentNullException"/>; <paramref name="numero"/> fuera de 1 a 60 ⇒
        /// <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        public static string NombreDeCuota(string nombreDelArreglo, int numero)
        {
            if (nombreDelArreglo == null) throw new ArgumentNullException(nameof(nombreDelArreglo));
            if (numero < 1 || numero > GeneradorDeCalendario.PlazoMaximo)
                throw new ArgumentOutOfRangeException(nameof(numero), numero, "El numero de cuota debe estar entre 1 y " + GeneradorDeCalendario.PlazoMaximo.ToString(CultureInfo.InvariantCulture) + ".");
            return nombreDelArreglo + "-C" + numero.ToString("D2", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// El calendario de un arreglo (2.5, <c>ArregloCalendarioStep</c>, pasos 3 a 5): <see cref="Montos.ReglasDeMontos.Calcular"/> y <see cref="GeneradorDeCalendario.Generar"/> con los valores del
        /// arreglo ya guardado. Un valor <c>null</c> es un dato que falta ⇒ <c>CARP-ARR-006</c> nombrando los que faltan, en este orden y separados por coma y espacio: <c>sanic_saldoinicial</c>,
        /// <c>sanic_montoabonado</c>, <c>sanic_cuotamensual</c>, <c>sanic_plazo</c>, <c>sanic_fechainicio</c>. Con todos presentes: un <paramref name="plazo"/> fuera de 1 a 60 lanza
        /// <see cref="ArgumentOutOfRangeException"/> (lo escribió SYSTEM: ni S-19 ni S-21 lo habrían dejado pasar; <c>CARP-ARR-003</c> necesita la capacidad, que este paso no lee) ANTES de calcular nada;
        /// si no, los errores de <c>ReglasDeMontos</c> (<c>CARP-ARR-007</c>, <c>-004</c>, <c>-005</c>, en ese orden) y, si va bien, las cuotas con el monto de cada una (<c>MontosDeCuotas</c>) y el vencimiento de
        /// <c>GeneradorDeCalendario</c>. Nunca lanza por una regla de negocio.
        /// </summary>
        public static PlanDeCalendario PlanificarCalendario(decimal? saldoInicial, decimal? montoAbonado, decimal? cuotaMensual, int? plazo, DateTime? fechaInicio)
        {
            var faltan = new List<string>();
            if (!saldoInicial.HasValue) faltan.Add(ColSaldoInicial);
            if (!montoAbonado.HasValue) faltan.Add(ColMontoAbonado);
            if (!cuotaMensual.HasValue) faltan.Add(ColCuotaMensual);
            if (!plazo.HasValue) faltan.Add(ColPlazo);
            if (!fechaInicio.HasValue) faltan.Add(ColFechaInicio);
            if (faltan.Count > 0) return PlanDeCalendario.Fallo(CodigosDeError.ARR_006.Crear(Lista(faltan)));

            // Un plazo fuera de rango lo escribio SYSTEM (S-19/S-21 no lo dejan pasar): no es una regla de negocio (5c, punto 7).
            if (plazo.Value < 1 || plazo.Value > GeneradorDeCalendario.PlazoMaximo)
                throw new ArgumentOutOfRangeException(nameof(plazo), plazo.Value, "El plazo debe estar entre 1 y " + GeneradorDeCalendario.PlazoMaximo.ToString(CultureInfo.InvariantCulture) + ".");

            var montos = ReglasDeMontos.Calcular(saldoInicial.Value, montoAbonado.Value, cuotaMensual.Value, plazo.Value);
            if (!montos.Ok) return PlanDeCalendario.Fallo(montos.Error);

            var vencimientos = GeneradorDeCalendario.Generar(fechaInicio.Value, plazo.Value, cuotaMensual.Value, montos.MontoUltimaCuota);
            var montosDeCuotas = montos.MontosDeCuotas();
            var cuotas = new List<CuotaDeCalendario>();
            for (int i = 0; i < vencimientos.Count; i++)
                cuotas.Add(new CuotaDeCalendario(vencimientos[i].Numero, vencimientos[i].FechaVencimiento, montosDeCuotas[i]));
            return PlanDeCalendario.Exito(cuotas);
        }

        /// <summary>
        /// Evalúa UN alta (<c>ArregloCrearStep</c>, 2.5, pasos 1 a 10; firma de 4.4). Nunca devuelve <c>null</c> ni lanza por una regla de negocio (eso es <see cref="ResultadoDeEvaluacion.Rechazar"/>); lanza
        /// <see cref="ArgumentNullException"/> si algún argumento es nulo. <paramref name="versionesVigentes"/> son las versiones con <c>sanic_estado</c> Vigente y <c>statecode</c> 0 (el step las lee con SYSTEM
        /// SOLO si quien escribe no es el sistema). El resultado, permitido, trae en <c>Escribir</c> lo que el step pone en el <c>Target</c>.
        /// <para>
        /// <b>Orden</b> (gana el primero que rechaza). (0) <c>Identidad.EsEscrituraDelSistema</c> (UNA lectura) ⇒ permitir sin escribir nada (DC-03; PA-ARR-11, incluso con columnas solo-sistema, estado distinto de
        /// Borrador, datos que faltan o ninguna versión). (1) <b>Estado</b> ⇒ <c>CARP-ARR-008</c> si el estado efectivo del <c>Target</c> no es Borrador: <c>statecode</c> y <c>statuscode</c> presentes se toman
        /// tal cual; con uno solo, el otro es el de Borrador (<c>statecode</c> ausente = Activo; <c>statuscode</c> ausente = el motivo por defecto de ese <c>statecode</c>, DC-06); un valor <c>null</c> cuenta como ausente;
        /// un par que no existe (p. ej. 0/2) o un <c>statecode</c> que no es 0 ni 1 es "no Borrador". Un <c>statecode = 1</c> solo se rechaza aunque la plataforma lo ignore en un Create (RESULTADO.md (6)): falla cerrado
        /// (decisión del 5c). (2) <b>Solo-sistema</b>: cada columna de <c>Columnas.De(TablaArreglo).SoloSistema</c> distinta de <c>sanic_nombre</c> que el <c>Target</c> trae con un valor (ni <c>null</c> ni texto vacío)
        /// ⇒ <c>CARP-ARR-009</c> nombrando todas las que vengan, en el orden de <see cref="ColumnasDeLaPreImagenDeActualizacion"/>, separadas por coma y espacio; <c>sanic_nombre</c> cuenta (y va en esa misma
        /// lista, en su lugar del orden) solo si trae un valor que NO cumple <see cref="NombreTieneFormatoDeAutonumerico"/> (un valor que no es texto tampoco lo cumple); un <c>sanic_nombre</c> ausente,
        /// nulo, vacío o con el formato no rechaza, y el step nunca lo toca (V-8: quitarlo o reemplazarlo dejaría la primaria vacía). (3) <b>Obligatorias</b> ⇒ <c>CARP-ARR-006</c> nombrando las que faltan, en este
        /// orden: <c>sanic_numeroproducto</c> (tras normalizar; ausente, nulo, vacío o de solo espacios es faltante), <c>sanic_saldoinicial</c>, <c>sanic_montoabonado</c>, <c>sanic_cuotamensual</c>,
        /// <c>sanic_plazo</c>, <c>sanic_fechainicio</c> (ausente o <c>null</c>). (4) Normaliza <c>sanic_numeroproducto</c> (<c>NormalizadorDeTexto.NumeroDeProducto</c>). (5) <c>versionesVigentes</c> vacía ⇒ <c>CARP-ARR-001</c>;
        /// más de una ⇒ <c>CARP-ARR-002</c>. (6) <b>Plazo</b>: fuera de 1 a 60 o mayor que la capacidad de esa versión ⇒ <c>CARP-ARR-003</c> con {máximo} = el menor entre 60 y la capacidad y {capacidad} = la capacidad.
        /// (7) <b>Canal</b>: <c>sanic_conversacionid</c> presente y no <c>null</c> con <c>sanic_canalorigen</c> ausente o <c>null</c> ⇒ escribe <c>CanalDeOrigen.Correo</c>; con canal, no lo toca; sin conversación, no escribe
        /// canal. (8) <b>Montos</b> con <c>ReglasDeMontos.Calcular</c> ⇒ <c>CARP-ARR-007</c>, <c>-004</c>, <c>-005</c>. (9) <c>sanic_fechafin</c> = <c>GeneradorDeCalendario.FechaFin(fechainicio, plazo)</c>; SOBRESCRIBE la que
        /// traiga el <c>Target</c> (OA-5). (10) <c>sanic_cantidadreaperturas</c> = 0.
        /// </para>
        /// <para>
        /// <b>Escribir</b> (valores neutros) cuando se permite a una persona: <c>sanic_numeroproducto</c> (normalizado), <c>sanic_versionplantillaid</c> = <c>ReferenciaDeEntidad("sanic_carp_tbl_versionplantilla", id)</c>,
        /// <c>sanic_canalorigen</c> (solo si se completa) = <c>ValorDeOpcion(CanalDeOrigen.Correo)</c>, <c>sanic_saldoremanente</c> y <c>sanic_totalpagar</c> (<c>decimal</c>), <c>sanic_fechafin</c> (<c>DateTime</c>, solo la
        /// fecha) y <c>sanic_cantidadreaperturas</c> = 0 (<c>int</c>). Ninguna otra clave.
        /// </para>
        /// <para>
        /// <b>Tipos de entrada.</b> Los decimales pueden llegar como <c>decimal</c> o <c>ValorDeDinero</c>, los choices como <c>ValorDeOpcion</c> o <c>int</c>; el plazo, como <c>int</c>. Un valor de otro tipo cuenta como faltante.
        /// </para>
        /// </summary>
        public static ResultadoDeEvaluacion EvaluarAlta(PedidoDeAlta pedido, IReadOnlyList<VersionVigente> versionesVigentes)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));
            if (versionesVigentes == null) throw new ArgumentNullException(nameof(versionesVigentes));
            var target = pedido.Target;

            // Paso 0 (DC-03): el sistema no se revalida, ni siquiera lee las versiones.
            if (pedido.Identidad.EsEscrituraDelSistema) return ResultadoDeEvaluacion.Permitir();

            // Paso 1: el estado efectivo del alta debe ser Borrador (DC-06); un statecode = 1 solo falla cerrado (5c, punto 2).
            var borrador = Estados.De(EstadoDeArreglo.Borrador);
            if (!EstadoDestino(target, borrador).Equals(borrador)) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_008.Crear());

            // Paso 2: solo-sistema con valor, en el orden de la lista de 43 (5c, punto 3).
            var tabla = Columnas.De(Columnas.TablaArreglo);
            var soloSistema = new List<string>();
            foreach (var columna in PreImagenDeActualizacion)
            {
                if (tabla.CategoriaDe(columna) != CategoriaDeColumna.SoloSistema) continue;
                object valor;
                if (!target.TryGetValue(columna, out valor) || valor == null) continue;
                var texto = valor as string;
                if (texto != null && texto.Length == 0) continue;
                // sanic_nombre lo completa la plataforma (V-8): solo cuenta si NO tiene el formato del autonumerico.
                if (string.Equals(columna, ColNombre, StringComparison.Ordinal) && texto != null && NombreTieneFormatoDeAutonumerico(texto)) continue;
                soloSistema.Add(columna);
            }
            if (soloSistema.Count > 0) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_009.Crear(Lista(soloSistema)));

            // Paso 3: obligatorias para poder calcular.
            var numeroDeProducto = NormalizadorDeTexto.NumeroDeProducto(Valor(target, ColNumeroProducto) as string);
            var saldoInicial = Monto(Valor(target, ColSaldoInicial));
            var montoAbonado = Monto(Valor(target, ColMontoAbonado));
            var cuotaMensual = Monto(Valor(target, ColCuotaMensual));
            var plazo = EnteroPlano(Valor(target, ColPlazo));
            var fechaInicio = Fecha(Valor(target, ColFechaInicio));
            var faltan = new List<string>();
            if (numeroDeProducto == null) faltan.Add(ColNumeroProducto);
            if (!saldoInicial.HasValue) faltan.Add(ColSaldoInicial);
            if (!montoAbonado.HasValue) faltan.Add(ColMontoAbonado);
            if (!cuotaMensual.HasValue) faltan.Add(ColCuotaMensual);
            if (!plazo.HasValue) faltan.Add(ColPlazo);
            if (!fechaInicio.HasValue) faltan.Add(ColFechaInicio);
            if (faltan.Count > 0) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_006.Crear(Lista(faltan)));

            // Pasos 4 y 5: la version vigente (exactamente una).
            if (versionesVigentes.Count == 0) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_001.Crear());
            if (versionesVigentes.Count > 1) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_002.Crear());
            var version = versionesVigentes[0];

            // Paso 6: el plazo, entre 1 y 60 y sin pasar de la capacidad (5c, punto 4).
            // 2.5 punto 9 (cierre 5c): el diccionario fija la capacidad en [1..60], asi que un valor menor que 1 es "sin capacidad" y solo rige el rango de 1 a 60, como en la edicion.
            var tope = version.Capacidad < 1 ? GeneradorDeCalendario.PlazoMaximo : version.Capacidad;
            if (plazo.Value < 1 || plazo.Value > GeneradorDeCalendario.PlazoMaximo || plazo.Value > tope)
                return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_003.Crear(Math.Min(GeneradorDeCalendario.PlazoMaximo, tope), tope));

            // Paso 8: montos (el paso 7, el canal, solo escribe).
            var montos = ReglasDeMontos.Calcular(saldoInicial.Value, montoAbonado.Value, cuotaMensual.Value, plazo.Value);
            if (!montos.Ok) return ResultadoDeEvaluacion.Rechazar(montos.Error);

            var escribir = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ColNumeroProducto, numeroDeProducto },
                { ColVersion, new ReferenciaDeEntidad(Columnas.TablaVersionDePlantilla, version.Id) },
                { ColSaldoRemanente, montos.SaldoRemanente },
                { ColTotalPagar, montos.TotalPagar },
                { ColFechaFin, GeneradorDeCalendario.FechaFin(fechaInicio.Value, plazo.Value) },   // OA-5: sobrescribe la del Target
                { ColCantidadReaperturas, 0 }
            };

            // Paso 7: con conversacion y sin canal, el canal es Correo (DD-13).
            if (Valor(target, ColConversacion) != null && Valor(target, ColCanalDeOrigen) == null)
                escribir[ColCanalDeOrigen] = new ValorDeOpcion((int)CanalDeOrigen.Correo);

            return ResultadoDeEvaluacion.Permitir(escribir);
        }

        /// <summary>
        /// Evalúa UN guardado de un arreglo existente (<c>ArregloActualizarStep</c>, 2.6; firma de 4.4). Nunca devuelve <c>null</c> ni lanza por una regla de negocio; lanza <see cref="ArgumentNullException"/> si
        /// algún argumento es nulo y <see cref="ArgumentException"/> si la pre-imagen no trae <c>statecode</c> y <c>statuscode</c>, o trae un par que no existe (tabla 0.7).
        /// <para>
        /// <b>Convenciones.</b> (a) "Cambió" es <c>Cambios.Distinto</c> (DC-01): la clave está en el <c>Target</c> Y su valor difiere del de la pre-imagen; <c>""</c> y <c>null</c> son lo mismo, los decimales se comparan
        /// por valor y las fechas por ticks. (b) <b>Una clave AUSENTE de la pre-imagen es "valor desconocido" y cuenta como cambio, aunque el <c>Target</c> traiga <c>null</c></b> (DC-01 punto 1: la regla recibe DC-01
        /// puro). La excepción "ausente = nulo" NO está aquí: la aplica el STEP completando la pre-imagen (Dev omite las nulas: las columnas NO protegidas de las 43 que falten pasan a <c>null</c>; las protegidas
        /// se leen con SYSTEM, <see cref="PedidoDeActualizacion.PreImagen"/>), decisión del orquestador para el 5c. (c) Las columnas del
        /// <c>Target</c> que no son de <see cref="ColumnasDeLaPreImagenDeActualizacion"/> (<c>ownerid</c>, <c>modifiedon</c>, el id primario…) no cuentan nunca. (d) <b>"Congelado"</b> es
        /// <c>sanic_fechacongelamiento</c> con un valor no nulo en la pre-imagen (ausente = sin congelar: es una lectura del estado, no una detección de cambios). (e) <c>sanic_numeroproducto</c> se compara
        /// NORMALIZADO (<c>NormalizadorDeTexto.NumeroDeProducto</c>) contra la pre-imagen, que siempre lo guarda normalizado: reenviar <c>" ab12 "</c> sobre <c>"AB12"</c> no es un cambio. (f) La identidad se
        /// consulta (<c>EsEscrituraDelSistema</c>, UNA lectura) solo cuando el paso 0 vio algo que decidir; <c>EsJefatura()</c>, a lo sumo una vez y solo en los gestos Reabrir; <c>NumerosDelCredito</c>, una vez y
        /// solo en Formalizar; <c>CapacidadDeLaVersion</c>, una vez y solo si cambió una de las cuatro columnas de los montos; <c>TienePagos</c>, una vez y solo si cambió una columna del calendario.
        /// (g) <c>ahora</c> es <c>pedido.AhoraUtc</c> y el actor es <c>Identidad.ActorId</c>. (h) <b>Normalización del número de producto:</b> si el <c>Target</c> trae <c>sanic_numeroproducto</c> como texto y su forma
        /// normalizada es distinta del valor crudo, TODO resultado permitido (también "nada cambió" y también el de una edición que no lo cambia) incluye <c>sanic_numeroproducto</c> normalizado en <c>Escribir</c>, para que
        /// la plataforma no guarde el crudo; si ya está normalizado no se escribe. Un rechazo no escribe nada; el sistema (DC-03) tampoco, cuando la regla llegó a preguntar por la identidad (en "nada cambió" no se pregunta).
        /// </para>
        /// <para>
        /// <b>Estado destino (DC-06 y DC-29, decisión del 5c).</b> Es el que la plataforma dejaría, no solo el que el <c>Target</c> dice: con <c>statecode</c> y <c>statuscode</c> en el <c>Target</c>, ese par; con solo
        /// <c>statecode</c>, ese estado con su motivo POR DEFECTO (<c>Estados.StatusPorDefecto</c>: Activo = Borrador, Inactivo = Terminado) AUNQUE sea el mismo que el de la pre-imagen; con solo <c>statuscode</c>,
        /// el <c>statecode</c> de la pre-imagen con ese motivo; con ninguno, el de la pre-imagen. Un valor <c>null</c> cuenta como ausente. Un <c>statecode</c> que no es 0 ni 1 no tiene motivo por defecto y queda como
        /// un par que no existe. <b>El estado cambió</b> si el destino difiere del de la pre-imagen. Así, reenviar <c>statecode = 0</c> a un Vigente es un Vigente → Borrador (<c>CARP-ARR-010</c>) y reenviar
        /// <c>statecode = 1</c> a un Anulado o a un Descartado es un cambio a Terminado (<c>CARP-ARR-022</c>); reenviar su <c>statecode</c> a un Borrador o a un Terminado sigue sin ser un cambio. Se descartó
        /// completar el <c>Target</c> con el <c>statuscode</c> de la pre-imagen (escribe algo que la persona no mandó y oculta el intento) y rechazar todo reenvío (rompe a quien reenvía la fila completa). El
        /// sistema no queda protegido: ninguna escritura del diseño manda solo <c>statecode</c> (0.7).
        /// </para>
        /// <para>
        /// <b>Pasos</b> (gana el primero que rechaza). <b>0.</b> Columnas contadas = las de <see cref="ColumnasDeLaPreImagenDeActualizacion"/> menos <c>statecode</c>, <c>statuscode</c> y <c>sanic_conversacionid</c>
        /// (la única columna libre), más <c>sanic_conversacionid</c> cuando la pre-imagen es Terminado, Anulado o Descartado (V-2). Los gestos Desbloquear y Reabrir (un Vigente con <c>sanic_motivoreapertura</c> en el <c>Target</c>) se reconocen por lo que el <c>Target</c> TRAE, no por "cambió" (paso 2): su
        /// disparador cuenta como "algo que decidir" aunque el valor sea igual al de la pre-imagen. Si el estado no cambió, ninguna columna contada cambió y no hay disparador ⇒ permitir sin escribir y SIN
        /// consultar la identidad. Si cambió algo o hay disparador y <c>EsEscrituraDelSistema</c> ⇒ permitir sin escribir (DC-03: así pasan la generación del formato, el paso a Terminado y el repunte al unir).
        /// </para>
        /// <para>
        /// <b>1. Estados inactivos.</b> Anulado o Descartado ⇒ <c>CARP-ARR-022</c> (cualquier cambio de los del paso 0). Terminado ⇒ <c>CARP-ARR-022</c> salvo que el destino sea Vigente (el gesto Reabrir): un
        /// Terminado → Borrador, → Anulado o → un par que no existe da <c>-022</c> aquí. Con <c>statecode = 0</c> y <c>statuscode</c> Vigente en el <c>Target</c> el destino es Vigente; con solo uno de los dos, no.
        /// </para>
        /// <para>
        /// <b>2. Gesto</b> (lo que quedó: origen Borrador, Vigente o el Terminado que reabre). Con cambio de estado, el gesto es: Borrador → Vigente <b>Formalizar</b>; Borrador → Descartado <b>Descartar</b>;
        /// Vigente → Anulado <b>Anular</b>; Terminado → Vigente <b>ReabrirTerminado</b>; CUALQUIER otro destino (Vigente → Borrador o Terminado, Borrador → Anulado o Terminado, un par que no existe) ⇒
        /// <c>CARP-ARR-010</c>, que gana a todo lo demás (también a <c>-012</c>). Sin cambio de estado y con origen Vigente: el <c>Target</c> trae <c>sanic_motivoreapertura</c> (presente, con cualquier valor, igual o no al guardado) ⇒ <b>ReabrirVigenteCongelado</b> (con otro
        /// origen no es un gesto: si el valor cambió sigue el paso 4 y da <c>-024</c>); el <c>Target</c> trae <c>sanic_fechacongelamiento</c> presente y <c>null</c> ⇒ <b>Desbloquear</b> (también con cambio de estado;
        /// así PA-ARR-31 y PA-ARR-35 valen aunque la pre-imagen ya tenga la columna vacía: los formularios mandan solo lo modificado y las Custom API escriben como SYSTEM, que no pasa por aquí). <b>Más de un
        /// gesto ⇒ <c>CARP-ARR-012</c></b> nombrando las columnas cambiadas (<c>Cambios.Distinto</c>) más las de disparador presentes, sobre la lista de 43, en su orden. Ninguno ⇒ paso 4.
        /// </para>
        /// <para>
        /// <b>3. Reglas del gesto.</b> Cada gesto admite SOLO sus columnas: <c>statecode</c>, <c>statuscode</c> y las suyas (Formalizar: ninguna más; Descartar: <c>sanic_motivodescarte</c>; Anular:
        /// <c>sanic_motivoanulacion</c>; los dos Reabrir: <c>sanic_motivoreapertura</c>; Desbloquear: <c>sanic_fechacongelamiento</c>); cualquier otra columna contada que cambió, incluida
        /// <c>sanic_conversacionid</c> (DC-01; reenviar el mismo valor no cuenta) ⇒ <c>CARP-ARR-012</c> nombrándolas (D-40: Reabrir es puro). Esa comprobación va PRIMERO en cada gesto. Después, por gesto y en este
        /// orden: <b>Formalizar</b>: sin <c>sanic_fechacongelamiento</c> o sin <c>sanic_numeroprovisional</c> en la pre-imagen ⇒ <c>-019</c>; <c>ConsecutivoDeCredito.Siguiente(NumerosDelCredito(producto,
        /// numeroproducto))</c> con el producto y el número de la pre-imagen ⇒ si da error, <c>-023</c>; si el siguiente es distinto del provisional ⇒ <c>-020</c>. Escribe <c>sanic_numeroarreglo</c> = provisional (int),
        /// <c>sanic_formalizadoporid</c> = <c>ReferenciaDeEntidad("systemuser", actor)</c>, <c>sanic_fechaformalizado</c> = ahora (no toca el provisional ni <c>sanic_fechaformalizacion</c>, P-07). <b>Descartar</b>:
        /// <c>sanic_motivodescarte</c> del <c>Target</c> vacío o de solo espacios ⇒ <c>-013</c>; escribe <c>sanic_descartadoporid</c>, <c>sanic_fechadescarte</c> = ahora y <c>sanic_fechatermino</c> = ahora.
        /// <b>Anular</b>: <c>sanic_motivoanulacion</c> vacío ⇒ <c>-014</c>; escribe <c>sanic_anuladoporid</c>, <c>sanic_fechaanulacion</c> = ahora y <c>sanic_fechatermino</c> = ahora. <b>ReabrirTerminado</b>: motivo de
        /// reapertura vacío ⇒ <c>-015</c>; <c>jefatura.EsJefatura()</c> falso ⇒ <c>-016</c>. <b>ReabrirVigenteCongelado</b>: sin congelar ⇒ <c>-017</c>; motivo vacío ⇒ <c>-015</c>; no jefatura ⇒ <c>-016</c>. Los dos Reabrir
        /// escriben <c>sanic_reabiertoporid</c> = actor (<c>systemuser</c>), <c>sanic_fechareapertura</c> = ahora, <c>sanic_cantidadreaperturas</c> = el de la pre-imagen (ausente = 0) + 1, y <c>null</c> en
        /// <c>sanic_fechatermino</c>, <c>sanic_fechacongelamiento</c>, <c>sanic_instantaneanombre</c>, <c>sanic_instantaneacedula</c>, <c>sanic_instantaneacorreo</c>, <c>sanic_instantaneacelular</c> y
        /// <c>sanic_numeroprovisional</c> (10 claves); NUNCA <c>statecode</c> ni <c>statuscode</c> (el Vigente lo trae el <c>Target</c>, V-5). <b>Desbloquear</b>: origen Vigente ⇒ <c>-018</c> (esté o no congelado);
        /// origen Borrador sin congelar ⇒ <c>-017</c>; si no, escribe <c>null</c> en las cuatro <c>sanic_instantanea*</c> y en <c>sanic_numeroprovisional</c> (5 claves). Una columna de motivo que no es la del gesto ⇒ <c>-012</c>.
        /// </para>
        /// <para>
        /// <b>4. Edición</b> (origen Borrador o Vigente, sin gesto, sin cambio de estado). (1) Cambió alguna solo-sistema (incluida <c>sanic_nombre</c>, sea cual sea su formato) ⇒ <c>CARP-ARR-009</c> nombrándolas; si no,
        /// cambió un motivo (descarte, anulación o reapertura) ⇒ <c>CARP-ARR-024</c>. (2) Congelado y cambió una columna de datos del acuerdo ⇒ <c>CARP-ARR-011</c> nombrando TODAS las que cambiaron (una o más, en el
        /// orden de la lista de 43). (3) <c>sanic_numeroproducto</c> en el <c>Target</c> ⇒ lo escribe normalizado; si queda vacío, <c>CARP-ARR-006</c> nombrándolo. (4) Si cambió <c>sanic_saldoinicial</c>,
        /// <c>sanic_montoabonado</c>, <c>sanic_cuotamensual</c> o <c>sanic_plazo</c>: con los valores EFECTIVOS (el del <c>Target</c> si viene, si no el de la pre-imagen) y, si alguno de los cuatro es nulo,
        /// <c>CARP-ARR-006</c> nombrándolo; plazo fuera de 1 a 60 o mayor que <c>Lecturas.CapacidadDeLaVersion(sanic_versionplantillaid de la pre-imagen)</c> ⇒ <c>CARP-ARR-003</c> (mismos marcadores que el alta;
        /// capacidad <c>null</c> ⇒ solo el rango 1 a 60, con {máximo} = {capacidad} = 60); después <c>ReglasDeMontos.Calcular</c> ⇒ <c>-007</c>, <c>-004</c>, <c>-005</c>; escribe <c>sanic_saldoremanente</c> y
        /// <c>sanic_totalpagar</c>. (5) Si cambió alguna de esas cuatro, <c>sanic_fechainicio</c> o <c>sanic_fechafin</c>: escribe <c>sanic_fechafin</c> = <c>FechaFin(fechainicio, plazo)</c> efectivos (sobrescribe la que
        /// traiga el <c>Target</c>, OA-5); fechainicio nula ⇒ <c>CARP-ARR-006</c>. (6) Si cambió <c>sanic_plazo</c>, <c>sanic_cuotamensual</c>, <c>sanic_fechainicio</c>, <c>sanic_saldoinicial</c> o
        /// <c>sanic_montoabonado</c> y <c>Lecturas.TienePagos(ArregloId)</c> ⇒ <c>CARP-ARR-021</c> (después de los errores de montos). Sin nada de lo anterior, permite sin escribir (<c>PA-ARR-12</c>).
        /// </para>
        /// </summary>
        public static ResultadoDeEvaluacion EvaluarActualizacion(PedidoDeActualizacion pedido, IConsecutivos consecutivos, IJefatura jefatura)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));
            if (consecutivos == null) throw new ArgumentNullException(nameof(consecutivos));
            if (jefatura == null) throw new ArgumentNullException(nameof(jefatura));
            var target = pedido.Target;

            var origen = EstadoDeLaPreImagen(pedido.PreImagen);
            var actual = Estados.De(origen);
            var destino = EstadoDestino(target, actual);
            var estadoCambio = !destino.Equals(actual);
            var cerrado = origen == EstadoDeArreglo.Terminado || origen == EstadoDeArreglo.Anulado || origen == EstadoDeArreglo.Descartado;

            // Paso 0: las columnas contadas que cambiaron, en el orden de la lista de 43. sanic_conversacionid (la unica libre) cuenta solo en un arreglo cerrado (V-2).
            var cambiadas = new List<string>();
            foreach (var columna in PreImagenDeActualizacion)
            {
                if (string.Equals(columna, ColEstado, StringComparison.Ordinal) || string.Equals(columna, ColMotivoDeEstado, StringComparison.Ordinal)) continue;
                if (string.Equals(columna, ColConversacion, StringComparison.Ordinal) && !cerrado) continue;
                if (Cambio(pedido, columna)) cambiadas.Add(columna);
            }

            // Los gestos Desbloquear y Reabrir se reconocen por lo que el Target TRAE, no por "cambio" (5c, punto 4): su disparador cuenta como algo que decidir.
            object valorPresente;
            // Cierre 5c, punto 2: el disparador solo existe en Borrador y Vigente; en un estado cerrado, reenviar el nulo sobre un nulo es un no-cambio (DC-01).
            var desbloquear = (origen == EstadoDeArreglo.Borrador || origen == EstadoDeArreglo.Vigente) && target.TryGetValue(ColCongelamiento, out valorPresente) && valorPresente == null;
            var reabrirVigente = origen == EstadoDeArreglo.Vigente && !estadoCambio && target.ContainsKey(ColMotivoReapertura);

            if (!estadoCambio && cambiadas.Count == 0 && !desbloquear && !reabrirVigente) return Cerrar(pedido, new Dictionary<string, object>(StringComparer.Ordinal));

            // DC-03: el sistema no se revalida (tambien para el estado que la plataforma reescribiria, DC-29).
            if (pedido.Identidad.EsEscrituraDelSistema) return ResultadoDeEvaluacion.Permitir();

            // Paso 1: estados inactivos. Un Terminado solo admite el gesto Reabrir (destino Vigente).
            if (origen == EstadoDeArreglo.Anulado || origen == EstadoDeArreglo.Descartado) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_022.Crear());
            if (origen == EstadoDeArreglo.Terminado && !destino.Equals(Estados.De(EstadoDeArreglo.Vigente))) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_022.Crear());

            // Paso 2: el gesto. Un cambio de estado que no es gesto da -010 y gana a todo lo demas, tambien a -012.
            var gestos = new List<Gesto>();
            if (estadoCambio)
            {
                switch (origen)
                {
                    case EstadoDeArreglo.Borrador:
                        if (destino.Equals(Estados.De(EstadoDeArreglo.Vigente))) gestos.Add(Gesto.Formalizar);
                        else if (destino.Equals(Estados.De(EstadoDeArreglo.Descartado))) gestos.Add(Gesto.Descartar);
                        else return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_010.Crear());
                        break;
                    case EstadoDeArreglo.Vigente:
                        if (destino.Equals(Estados.De(EstadoDeArreglo.Anulado))) gestos.Add(Gesto.Anular);
                        else return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_010.Crear());
                        break;
                    default:
                        gestos.Add(Gesto.ReabrirTerminado);
                        break;
                }
            }
            if (reabrirVigente) gestos.Add(Gesto.ReabrirVigente);
            if (desbloquear) gestos.Add(Gesto.Desbloquear);

            if (gestos.Count > 1)
            {
                var nombradas = new List<string>();
                foreach (var columna in PreImagenDeActualizacion)
                {
                    var esDisparador = (desbloquear && string.Equals(columna, ColCongelamiento, StringComparison.Ordinal))
                        || (reabrirVigente && string.Equals(columna, ColMotivoReapertura, StringComparison.Ordinal));
                    if (esDisparador || Cambio(pedido, columna)) nombradas.Add(columna);
                }
                return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_012.Crear(Lista(nombradas)));
            }

            if (gestos.Count == 1) return EvaluarGesto(pedido, gestos[0], consecutivos, jefatura);
            return EvaluarEdicion(pedido, cambiadas);
        }

        // ------------------------------------------------------------------ gestos (2.6, paso 3)

        private static ResultadoDeEvaluacion EvaluarGesto(PedidoDeActualizacion pedido, Gesto gesto, IConsecutivos consecutivos, IJefatura jefatura)
        {
            // Cada gesto admite SOLO sus columnas; esta comprobacion va PRIMERO (D-40: Reabrir es puro). Incluye sanic_conversacionid.
            var propia = ColumnaDelGesto(gesto);
            var otras = new List<string>();
            foreach (var columna in PreImagenDeActualizacion)
            {
                if (string.Equals(columna, ColEstado, StringComparison.Ordinal) || string.Equals(columna, ColMotivoDeEstado, StringComparison.Ordinal)) continue;
                if (propia != null && string.Equals(columna, propia, StringComparison.Ordinal)) continue;
                if (Cambio(pedido, columna)) otras.Add(columna);
            }
            if (otras.Count > 0) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_012.Crear(Lista(otras)));

            var preImagen = pedido.PreImagen;
            var ahora = pedido.AhoraUtc;
            var actor = new ReferenciaDeEntidad(TablaDeUsuarios, pedido.Identidad.ActorId);
            var escribir = new Dictionary<string, object>(StringComparer.Ordinal);

            switch (gesto)
            {
                case Gesto.Formalizar:
                {
                    var provisional = EnteroPlano(Valor(preImagen, ColNumeroProvisional));
                    if (Valor(preImagen, ColCongelamiento) == null || !provisional.HasValue) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_019.Crear());

                    // Cierre 5c (2.0.5): sin los datos del credito no hay consecutivo que calcular; -006 va despues de -012 y -019 y ANTES de consultar IConsecutivos.
                    var productoPrevio = Entero(Valor(preImagen, ColProducto));
                    var numeroPrevio = NormalizadorDeTexto.NumeroDeProducto(Valor(preImagen, ColNumeroProducto) as string);
                    var sinCredito = new List<string>();
                    if (!productoPrevio.HasValue) sinCredito.Add(ColProducto);
                    if (numeroPrevio == null) sinCredito.Add(ColNumeroProducto);
                    if (sinCredito.Count > 0) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_006.Crear(Lista(sinCredito)));

                    // El consecutivo se recalcula con el producto y el numero de la pre-imagen (2.0.5): otro arreglo del credito pudo formalizarse despues de imprimir.
                    var producto = (ProductoCrediticio)productoPrevio.Value;
                    var existentes = consecutivos.NumerosDelCredito(producto, numeroPrevio);
                    var siguiente = ConsecutivoDeCredito.Siguiente(existentes.Select(n => (int?)n));
                    if (!siguiente.Ok) return ResultadoDeEvaluacion.Rechazar(siguiente.Error);
                    if (siguiente.Numero != provisional.Value) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_020.Crear());

                    // No se toca el provisional ni sanic_fechaformalizacion (P-07).
                    escribir[ColNumeroArreglo] = provisional.Value;
                    escribir[ColFormalizadoPor] = actor;
                    escribir[ColFechaFormalizado] = ahora;
                    break;
                }
                case Gesto.Descartar:
                    if (string.IsNullOrWhiteSpace(Valor(pedido.Target, ColMotivoDescarte) as string)) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_013.Crear());
                    escribir[ColDescartadoPor] = actor;
                    escribir[ColFechaDescarte] = ahora;
                    escribir[ColFechaTermino] = ahora;
                    break;
                case Gesto.Anular:
                    if (string.IsNullOrWhiteSpace(Valor(pedido.Target, ColMotivoAnulacion) as string)) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_014.Crear());
                    escribir[ColAnuladoPor] = actor;
                    escribir[ColFechaAnulacion] = ahora;
                    escribir[ColFechaTermino] = ahora;
                    break;
                case Gesto.ReabrirTerminado:
                case Gesto.ReabrirVigente:
                    if (gesto == Gesto.ReabrirVigente && Valor(preImagen, ColCongelamiento) == null) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_017.Crear());
                    if (string.IsNullOrWhiteSpace(Valor(pedido.Target, ColMotivoReapertura) as string)) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_015.Crear());
                    if (!jefatura.EsJefatura()) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_016.Crear());

                    // 10 claves; NUNCA statecode ni statuscode: el Vigente lo trae el Target (V-5).
                    escribir[ColReabiertoPor] = actor;
                    escribir[ColFechaReapertura] = ahora;
                    escribir[ColCantidadReaperturas] = (EnteroPlano(Valor(preImagen, ColCantidadReaperturas)) ?? 0) + 1;
                    escribir[ColFechaTermino] = null;
                    escribir[ColCongelamiento] = null;
                    VaciarInstantanea(escribir);
                    break;
                default:
                    // Desbloquear: un Vigente no se desbloquea, este o no congelado; un Borrador sin congelar no tiene nada que desbloquear.
                    if (EstadoDeLaPreImagen(preImagen) == EstadoDeArreglo.Vigente) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_018.Crear());
                    if (Valor(preImagen, ColCongelamiento) == null) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_017.Crear());
                    VaciarInstantanea(escribir);
                    break;
            }
            return Cerrar(pedido, escribir);
        }

        private static string ColumnaDelGesto(Gesto gesto)
        {
            switch (gesto)
            {
                case Gesto.Descartar: return ColMotivoDescarte;
                case Gesto.Anular: return ColMotivoAnulacion;
                case Gesto.ReabrirTerminado:
                case Gesto.ReabrirVigente: return ColMotivoReapertura;
                case Gesto.Desbloquear: return ColCongelamiento;
                default: return null;
            }
        }

        // Las cuatro instantaneas y el provisional vuelven a nulo (2.6, Desbloquear y Reabrir).
        private static void VaciarInstantanea(Dictionary<string, object> escribir)
        {
            escribir[ColInstantaneaNombre] = null;
            escribir[ColInstantaneaCedula] = null;
            escribir[ColInstantaneaCorreo] = null;
            escribir[ColInstantaneaCelular] = null;
            escribir[ColNumeroProvisional] = null;
        }

        // ------------------------------------------------------------------ edicion (2.6, paso 4)

        private static ResultadoDeEvaluacion EvaluarEdicion(PedidoDeActualizacion pedido, List<string> cambiadas)
        {
            var tabla = Columnas.De(Columnas.TablaArreglo);
            var target = pedido.Target;
            var preImagen = pedido.PreImagen;

            // 1. Solo-sistema (incluida sanic_nombre, sea cual sea su formato) y despues los motivos sin su gesto.
            var soloSistema = cambiadas.Where(c => tabla.CategoriaDe(c) == CategoriaDeColumna.SoloSistema).ToList();
            if (soloSistema.Count > 0) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_009.Crear(Lista(soloSistema)));
            if (cambiadas.Any(c => string.Equals(c, ColMotivoDescarte, StringComparison.Ordinal) || string.Equals(c, ColMotivoAnulacion, StringComparison.Ordinal)
                || string.Equals(c, ColMotivoReapertura, StringComparison.Ordinal)))
                return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_024.Crear());

            // 2. Congelado: ninguna columna de datos del acuerdo (se nombran todas las que cambiaron).
            if (Valor(preImagen, ColCongelamiento) != null)
            {
                var datos = cambiadas.Where(c => tabla.CategoriaDe(c) == CategoriaDeColumna.DatosDelAcuerdo).ToList();
                if (datos.Count > 0) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_011.Crear(Lista(datos)));
            }

            var escribir = new Dictionary<string, object>(StringComparer.Ordinal);

            // 3. El numero de producto, normalizado; sin contenido no sirve para calcular.
            if (target.ContainsKey(ColNumeroProducto) && NormalizadorDeTexto.NumeroDeProducto(Valor(target, ColNumeroProducto) as string) == null)
                return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_006.Crear(ColNumeroProducto));

            // 4. Montos con los valores EFECTIVOS (el del Target si viene, si no el de la pre-imagen).
            var cambioMontos = Cambio(pedido, ColSaldoInicial) || Cambio(pedido, ColMontoAbonado) || Cambio(pedido, ColCuotaMensual) || Cambio(pedido, ColPlazo);
            if (cambioMontos)
            {
                var saldoInicial = Monto(Efectivo(pedido, ColSaldoInicial));
                var montoAbonado = Monto(Efectivo(pedido, ColMontoAbonado));
                var cuotaMensual = Monto(Efectivo(pedido, ColCuotaMensual));
                var plazo = EnteroPlano(Efectivo(pedido, ColPlazo));
                var faltan = new List<string>();
                if (!saldoInicial.HasValue) faltan.Add(ColSaldoInicial);
                if (!montoAbonado.HasValue) faltan.Add(ColMontoAbonado);
                if (!cuotaMensual.HasValue) faltan.Add(ColCuotaMensual);
                if (!plazo.HasValue) faltan.Add(ColPlazo);
                if (faltan.Count > 0) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_006.Crear(Lista(faltan)));

                // Capacidad ilegible (version inexistente o sin referencia): solo rige el rango de 1 a 60.
                var capacidad = CapacidadDeLaVersion(pedido);
                var tope = capacidad ?? GeneradorDeCalendario.PlazoMaximo;
                if (plazo.Value < 1 || plazo.Value > GeneradorDeCalendario.PlazoMaximo || plazo.Value > tope)
                    return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_003.Crear(Math.Min(GeneradorDeCalendario.PlazoMaximo, tope), tope));

                var montos = ReglasDeMontos.Calcular(saldoInicial.Value, montoAbonado.Value, cuotaMensual.Value, plazo.Value);
                if (!montos.Ok) return ResultadoDeEvaluacion.Rechazar(montos.Error);
                escribir[ColSaldoRemanente] = montos.SaldoRemanente;
                escribir[ColTotalPagar] = montos.TotalPagar;
            }

            // 5. sanic_fechafin se recalcula y SOBRESCRIBE la del Target (OA-5; union de columnas del 5c, punto 6).
            if (cambioMontos || Cambio(pedido, ColFechaInicio) || Cambio(pedido, ColFechaFin))
            {
                var fechaInicio = Fecha(Efectivo(pedido, ColFechaInicio));
                var plazo = EnteroPlano(Efectivo(pedido, ColPlazo));
                var faltan = new List<string>();
                if (!plazo.HasValue) faltan.Add(ColPlazo);
                if (!fechaInicio.HasValue) faltan.Add(ColFechaInicio);
                if (faltan.Count > 0) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_006.Crear(Lista(faltan)));
                escribir[ColFechaFin] = GeneradorDeCalendario.FechaFin(fechaInicio.Value, plazo.Value);
            }

            // 6. Un cambio del calendario con pagos registrados (DC-21), despues de los errores de montos.
            if ((cambioMontos || Cambio(pedido, ColFechaInicio)) && pedido.Lecturas.TienePagos(pedido.ArregloId))
                return ResultadoDeEvaluacion.Rechazar(CodigosDeError.ARR_021.Crear());

            return Cerrar(pedido, escribir);
        }

        // ------------------------------------------------------------------ apoyos

        private static object Valor(IReadOnlyDictionary<string, object> valores, string columna)
        {
            object valor;
            return valores.TryGetValue(columna, out valor) ? valor : null;
        }

        // El valor efectivo: el del Target si la clave viene (aunque sea null), si no el de la pre-imagen.
        private static object Efectivo(PedidoDeActualizacion pedido, string columna)
        {
            object valor;
            return pedido.Target.TryGetValue(columna, out valor) ? valor : Valor(pedido.PreImagen, columna);
        }

        // DC-01 puro (Cambios.Distinto): una clave AUSENTE de la pre-imagen es "desconocida" y cuenta como cambio. El numero de producto se compara NORMALIZADO (convencion e).
        private static bool Cambio(PedidoDeActualizacion pedido, string columna)
        {
            if (EsFechaDO(columna))
            {
                // Cierre 5c, punto 3: Dev trunca la fecha DO a la fecha, asi que se compara por .Date; si alguno no es DateTime vale DC-01.
                object nueva, previa;
                if (pedido.Target.TryGetValue(columna, out nueva) && pedido.PreImagen.TryGetValue(columna, out previa) && nueva is DateTime && previa is DateTime)
                    return ((DateTime)nueva).Date != ((DateTime)previa).Date;
            }

            if (!string.Equals(columna, ColNumeroProducto, StringComparison.Ordinal)) return Cambios.Distinto(pedido.Target, pedido.PreImagen, columna);

            object nuevo;
            if (!pedido.Target.TryGetValue(columna, out nuevo)) return false;
            var texto = nuevo as string;
            if (texto != null) nuevo = NormalizadorDeTexto.NumeroDeProducto(texto);
            object anterior;
            if (!pedido.PreImagen.TryGetValue(columna, out anterior)) return true;
            return Cambios.ValoresDistintos(nuevo, anterior);
        }

        private static bool EsFechaDO(string columna)
        {
            return string.Equals(columna, ColFechaInicio, StringComparison.Ordinal) || string.Equals(columna, ColFechaFin, StringComparison.Ordinal)
                || string.Equals(columna, ColFechaFormalizacion, StringComparison.Ordinal);
        }

        private static string Lista(IEnumerable<string> columnas)
        {
            return string.Join(", ", columnas);
        }

        private static int? Entero(object valor)
        {
            var opcion = valor as ValorDeOpcion;
            if (opcion != null) return opcion.Valor;
            if (valor is int) return (int)valor;
            return null;
        }

        private static int? EnteroPlano(object valor)
        {
            if (valor is int) return (int)valor;
            return null;
        }

        private static decimal? Monto(object valor)
        {
            var dinero = valor as ValorDeDinero;
            if (dinero != null) return dinero.Valor;
            if (valor is decimal) return (decimal)valor;
            return null;
        }

        private static DateTime? Fecha(object valor)
        {
            if (valor is DateTime) return (DateTime)valor;
            return null;
        }

        private static EstadoDeArreglo EstadoDeLaPreImagen(IReadOnlyDictionary<string, object> preImagen)
        {
            var estado = Entero(Valor(preImagen, ColEstado));
            var motivo = Entero(Valor(preImagen, ColMotivoDeEstado));
            if (!estado.HasValue || !motivo.HasValue)
                throw new ArgumentException("La pre-imagen de una actualizacion debe traer statecode y statuscode.", nameof(preImagen));
            var conocido = Estados.DeArreglo(new EstadoEfectivo(estado.Value, motivo.Value));
            if (!conocido.HasValue)
                throw new ArgumentException("La pre-imagen trae un par statecode/statuscode que no existe (diseno/03 0.7).", nameof(preImagen));
            return conocido.Value;
        }

        // DC-06 y DC-29: el estado que la plataforma dejaria, no solo el que el Target dice. `actual` es el de la pre-imagen (o Borrador en un alta).
        private static EstadoEfectivo EstadoDestino(IReadOnlyDictionary<string, object> target, EstadoEfectivo actual)
        {
            var estado = Entero(Valor(target, ColEstado));
            var motivo = Entero(Valor(target, ColMotivoDeEstado));
            if (motivo.HasValue) return new EstadoEfectivo(estado ?? actual.StateCode, motivo.Value);
            if (!estado.HasValue) return actual;
            // Un statecode que no es 0 ni 1 no tiene motivo por defecto: queda como un par que no existe (-1).
            var porDefecto = estado.Value == Estados.Activo || estado.Value == Estados.Inactivo ? Estados.StatusPorDefecto(estado.Value) : -1;
            return new EstadoEfectivo(estado.Value, porDefecto);
        }

        // sanic_capacidad de la version del arreglo (la de la pre-imagen); null si no hay referencia o la version no existe.
        private static int? CapacidadDeLaVersion(PedidoDeActualizacion pedido)
        {
            var version = Valor(pedido.PreImagen, ColVersion) as ReferenciaDeEntidad;
            if (version == null || version.Id == Guid.Empty) return null;
            return pedido.Lecturas.CapacidadDeLaVersion(version.Id);
        }

        // Todo resultado permitido de una persona lleva el numero de producto normalizado si el Target lo trae crudo (convencion h): la plataforma no debe guardar el crudo.
        private static ResultadoDeEvaluacion Cerrar(PedidoDeActualizacion pedido, Dictionary<string, object> escribir)
        {
            var crudo = Valor(pedido.Target, ColNumeroProducto) as string;
            if (crudo != null && !escribir.ContainsKey(ColNumeroProducto))
            {
                var normalizado = NormalizadorDeTexto.NumeroDeProducto(crudo);
                if (normalizado != null && !string.Equals(normalizado, crudo, StringComparison.Ordinal)) escribir[ColNumeroProducto] = normalizado;
            }
            return ResultadoDeEvaluacion.Permitir(escribir);
        }
    }
}
