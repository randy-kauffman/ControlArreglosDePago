using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Sanic.Carp.Plugins.Dominio.Calendario;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Dominio.Plantilla
{
    /// <summary>
    /// Las reglas del Mapeo de celda (diseno/03 2.8, firmas de 4.4; "Decisiones del arquitecto, 2026-10-19 (sublote 5e)"). Dominio puro: sin <c>Entity</c> ni SDK, sin estado, sin leer el reloj y con la
    /// identidad perezosa (0.3). Usa <c>Cambios</c>, <c>CodigosDeError</c> y <c>ResultadoDeEvaluacion</c>. DC-29 no aplica (Mapeo tiene los dos motivos por defecto). Las reglas ENTRE filas (dos campos en una
    /// celda, bloque completo, capacidad) no son de aquí: son de <see cref="ValidadorDePublicacion"/>.
    /// <para>
    /// <b>Esquema común</b> (violación primero, identidad después): la regla recorre sus comprobaciones EN MEMORIA, en orden, y se queda con la primera violación. Sin violación permite sin consultar la
    /// identidad (y, si corresponde, con el nombre calculado en <c>Escribir</c>). Con violación pregunta <c>Identidad.EsEscrituraDelSistema</c> UNA vez: sistema ⇒ permite sin escribir nada (DC-03); persona ⇒
    /// rechaza con esa violación. La versión padre se lee con SYSTEM antes que la identidad (la lectura no depende de quién escribe).
    /// </para>
    /// </summary>
    public static class ReglasDeMapeo
    {
        private static readonly IReadOnlyList<string> PreImagenDeActualizacion = new ReadOnlyCollection<string>(new[]
        {
            "sanic_versionplantillaid", "sanic_hoja", "sanic_celda", "sanic_tipo", "sanic_campo", "sanic_filasmaximas", "statecode", "statuscode", "sanic_nombre"
        });

        private static readonly IReadOnlyList<string> PreImagenDeBaja = new ReadOnlyCollection<string>(new[] { "sanic_versionplantillaid" });

        /// <summary>
        /// Las 9 columnas de la pre-imagen <c>PreImagen</c> de S-29 (seccion 5), en ese orden. Son las columnas VIGILADAS del Update: lo que no está aquí (<c>ownerid</c>, que rige S-13; <c>modifiedon</c>;
        /// el id primario) no cuenta como cambio.
        /// </summary>
        public static IReadOnlyList<string> ColumnasDeLaPreImagen { get { return PreImagenDeActualizacion; } }

        /// <summary>La única columna de la pre-imagen <c>PreImagen</c> de S-30 (seccion 5): <c>sanic_versionplantillaid</c>.</summary>
        public static IReadOnlyList<string> ColumnasDeLaPreImagenDeBaja { get { return PreImagenDeBaja; } }

        /// <summary>
        /// El nombre calculado de un mapeo (2.8): <c>hoja + "!" + celda</c>, tal cual (la hoja NO se recorta: la real empieza con un espacio, spike 1 hallazgo 1) y con la celda ya normalizada. Ejemplo:
        /// <c>(" FORMATO ARP ELECTRONICO", "B7")</c> ⇒ <c>" FORMATO ARP ELECTRONICO!B7"</c>. Una hoja o celda nula se trata como texto vacío. Sin cultura.
        /// </summary>
        public static string NombreCalculado(string hoja, string celda)
        {
            return (hoja ?? string.Empty) + "!" + (celda ?? string.Empty);
        }

        /// <summary>
        /// S-28 (2.8, <c>MapeoCeldaCrearStep</c>). Violaciones, en este orden:
        /// <list type="number">
        /// <item>Versión padre (<c>sanic_versionplantillaid</c> del <c>Target</c>, <c>ReferenciaDeEntidad</c> o <c>Guid</c>) ausente, nula o cuyo <c>Lecturas.EstadoDeLaVersion</c> no es Borrador (también <c>null</c>) ⇒ <c>CARP-MAP-001</c>. Sin padre no se lee nada.</item>
        /// <item>Celda (<c>sanic_celda</c> del <c>Target</c>) nula, vacía o que no es A1 válida (letras ASCII de A a XFD y fila 1 a 1048576 sin ceros a la izquierda, sin espacios, <c>$</c> ni rangos; mayúscula o minúscula) ⇒ <c>CARP-MAP-002</c> con la celda tal como llegó (vacía si es nula).</item>
        /// <item>(<c>sanic_tipo</c>, <c>sanic_campo</c>) fuera de la lista blanca de dicc. §3.1 columna 4 (las 19 primeras opciones y Moneda solo con ValorSimple; las cinco <c>Cuota: …</c> solo con ColumnaDeBloqueDeCuotas; las dos <c>Total: …</c> solo con TotalCalculado; tipo o campo nulos o no definidos no son compatibles con nada) ⇒ <c>CARP-MAP-003</c> con la etiqueta del campo con tildes ("Cuota: monto"), el número si el campo no está definido o <c>(vacío)</c> si falta.</item>
        /// <item>Tipo ColumnaDeBloqueDeCuotas y <c>sanic_filasmaximas</c> nulo o fuera de 1 a 60 ⇒ <c>CARP-MAP-004</c>. Con otro tipo, filas máximas no se mira.</item>
        /// <item>Hoja (<c>sanic_hoja</c>) nula, vacía o solo espacios ⇒ <c>CARP-MAP-005</c>. NO se recorta: <c>" X"</c> es válida.</item>
        /// </list>
        /// Sin violación ⇒ permite con <c>Escribir</c> = <c>sanic_celda</c> en mayúscula y <c>sanic_nombre</c> = <see cref="NombreCalculado"/>, reemplazando lo que la persona haya enviado (el Create no rechaza el nombre).
        /// Con violación: sistema ⇒ permite sin escribir; persona ⇒ rechaza. <c>pedido</c> nulo ⇒ <see cref="ArgumentNullException"/>.
        /// </summary>
        public static ResultadoDeEvaluacion EvaluarAlta(PedidoDeAltaDeMapeo pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));

            var target = pedido.Target;
            ErrorDeNegocio violacion = ErrorDelPadre(Valor(target, ColVersion), pedido.Lecturas)
                ?? ErrorDeLosValores(Valor(target, ColHoja), Valor(target, ColCelda), Valor(target, ColTipo), Valor(target, ColCampo), Valor(target, ColFilasMaximas));
            if (violacion != null) return ResolverViolacion(violacion, pedido.Identidad);

            return Escribiendo(Valor(target, ColHoja), Valor(target, ColCelda));
        }

        /// <summary>
        /// S-29 (2.8, <c>MapeoCeldaActualizarStep</c>). Primero las columnas cambiadas entre las 9 de <see cref="ColumnasDeLaPreImagen"/> (DC-01: presente en el <c>Target</c> Y distinto de la pre-imagen; ausente de
        /// la pre-imagen = desconocido = cambio; <c>""</c> es <c>null</c>; los textos son ordinales: <c>"b7"</c> contra <c>"B7"</c> SÍ es un cambio). Ninguna ⇒ permite sin leer nada ni consultar la identidad.
        /// Violaciones, en este orden (las cuatro primeras miran lo que cambió; las cinco últimas, los valores EFECTIVOS: el del <c>Target</c> si la clave viene, aunque sea <c>null</c>; si no, el de la pre-imagen):
        /// <list type="number">
        /// <item><c>CARP-MAP-001</c>: la versión padre de la PRE-imagen (<c>sanic_versionplantillaid</c>) falta o no está en Borrador (<c>Lecturas.EstadoDeLaVersion</c>, una vez). Gana a todo lo demás.</item>
        /// <item><c>CARP-MAP-007</c>: cambió <c>statecode</c> o <c>statuscode</c> (los dos nativos solo los cambia SYSTEM; reenviar los mismos valores no es un cambio).</item>
        /// <item><c>CARP-MAP-008</c>: cambió <c>sanic_nombre</c> (incluso a <c>null</c> o <c>""</c> contra un nombre con valor). Mira solo lo que mandó la persona, no lo que el plugin calcularía.</item>
        /// <item><c>CARP-MAP-006</c>: cambió <c>sanic_versionplantillaid</c> (decisión del arquitecto sobre el orden: el diseño no fijaba dónde iba).</item>
        /// <item><c>CARP-MAP-002</c>, <c>-003</c>, <c>-004</c> y <c>-005</c>: las mismas comprobaciones de <see cref="EvaluarAlta"/> (pasos 2 a 5), con valores efectivos.</item>
        /// </list>
        /// Sin violación ⇒ permite; si cambió <c>sanic_hoja</c> o <c>sanic_celda</c> (relativo a la pre-imagen) devuelve en <c>Escribir</c> <c>sanic_celda</c> en mayúscula y <c>sanic_nombre</c> = <see cref="NombreCalculado"/> con los
        /// valores efectivos, también si la persona reenvió el nombre de la pre-imagen; si no cambió ninguna de las dos, <c>Escribir</c> queda vacío (no se recalcula). Con violación: sistema ⇒ permite sin escribir; persona ⇒
        /// rechaza. <c>pedido</c> nulo ⇒ <see cref="ArgumentNullException"/>.
        /// </summary>
        public static ResultadoDeEvaluacion EvaluarActualizacion(PedidoDeMapeo pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));

            var cambiadas = Cambios.Cambiadas(pedido.Target, pedido.PreImagen, PreImagenDeActualizacion);
            if (cambiadas.Count == 0) return ResultadoDeEvaluacion.Permitir();

            // El orden lo fija 2.8 (y la decisión del arquitecto sobre -006): padre de la pre-imagen, estado, nombre, versión y luego los valores efectivos.
            ErrorDeNegocio violacion = ErrorDelPadre(Valor(pedido.PreImagen, ColVersion), pedido.Lecturas);
            if (violacion == null && (Contiene(cambiadas, ColEstado) || Contiene(cambiadas, ColMotivoDeEstado))) violacion = CodigosDeError.MAP_007.Crear();
            if (violacion == null && Contiene(cambiadas, ColNombre)) violacion = CodigosDeError.MAP_008.Crear();
            if (violacion == null && Contiene(cambiadas, ColVersion)) violacion = CodigosDeError.MAP_006.Crear();

            object hoja = Efectivo(pedido, ColHoja);
            object celda = Efectivo(pedido, ColCelda);
            if (violacion == null) violacion = ErrorDeLosValores(hoja, celda, Efectivo(pedido, ColTipo), Efectivo(pedido, ColCampo), Efectivo(pedido, ColFilasMaximas));
            if (violacion != null) return ResolverViolacion(violacion, pedido.Identidad);

            // El nombre se recalcula solo si cambió la hoja o la celda; si no, no se escribe nada (no se recalcula).
            if (!Contiene(cambiadas, ColHoja) && !Contiene(cambiadas, ColCelda)) return ResultadoDeEvaluacion.Permitir();
            return Escribiendo(hoja, celda);
        }

        /// <summary>
        /// S-30 (2.8, <c>MapeoCeldaEliminarStep</c>). Única violación: la versión padre de la pre-imagen (<c>sanic_versionplantillaid</c>) falta o su <c>Lecturas.EstadoDeLaVersion</c> no es Borrador ⇒ <c>CARP-MAP-001</c>.
        /// Con violación: sistema ⇒ permite; persona ⇒ rechaza. Sin violación permite sin consultar la identidad. Nunca escribe. <c>pedido</c> nulo ⇒ <see cref="ArgumentNullException"/>.
        /// </summary>
        public static ResultadoDeEvaluacion EvaluarBaja(PedidoDeBajaDeMapeo pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));

            ErrorDeNegocio violacion = ErrorDelPadre(Valor(pedido.PreImagen, ColVersion), pedido.Lecturas);
            return violacion == null ? ResultadoDeEvaluacion.Permitir() : ResolverViolacion(violacion, pedido.Identidad);
        }

        private const string ColVersion = "sanic_versionplantillaid";
        private const string ColHoja = "sanic_hoja";
        private const string ColCelda = "sanic_celda";
        private const string ColTipo = "sanic_tipo";
        private const string ColCampo = "sanic_campo";
        private const string ColFilasMaximas = "sanic_filasmaximas";
        private const string ColEstado = "statecode";
        private const string ColMotivoDeEstado = "statuscode";
        private const string ColNombre = "sanic_nombre";

        private static object Valor(IReadOnlyDictionary<string, object> valores, string columna)
        {
            object valor;
            return valores.TryGetValue(columna, out valor) ? valor : null;
        }

        // El valor efectivo: el del Target si la clave viene (aunque sea null); si no, el de la pre-imagen; ausente cuenta como nulo.
        private static object Efectivo(PedidoDeMapeo pedido, string columna)
        {
            object valor;
            if (pedido.Target.TryGetValue(columna, out valor)) return valor;
            return pedido.PreImagen.TryGetValue(columna, out valor) ? valor : null;
        }

        private static bool Contiene(IReadOnlyList<string> columnas, string columna)
        {
            for (int i = 0; i < columnas.Count; i++)
                if (string.Equals(columnas[i], columna, StringComparison.Ordinal)) return true;
            return false;
        }

        // Falla cerrado: sin padre, o con un padre que no se lee o no es Borrador, es CARP-MAP-001. Sin padre no se lee nada.
        private static ErrorDeNegocio ErrorDelPadre(object padre, ILecturasDeVersion lecturas)
        {
            Guid? id = null;
            var referencia = padre as ReferenciaDeEntidad;
            if (referencia != null) id = referencia.Id;
            else if (padre is Guid) id = (Guid)padre;
            if (!id.HasValue) return CodigosDeError.MAP_001.Crear();

            EstadoDeVersionDePlantilla? estado = lecturas.EstadoDeLaVersion(id.Value);
            return estado.HasValue && estado.Value == EstadoDeVersionDePlantilla.Borrador ? null : CodigosDeError.MAP_001.Crear();
        }

        // Pasos 2 a 5 de EvaluarAlta, con los valores que se le den (los del Target en el alta, los efectivos en la actualización).
        private static ErrorDeNegocio ErrorDeLosValores(object hoja, object celda, object tipo, object campo, object filasMaximas)
        {
            string textoDeCelda = celda as string;
            int columnaA1, filaA1;
            if (!ValidadorDePublicacion.TryParsearCeldaA1(textoDeCelda, out columnaA1, out filaA1)) return CodigosDeError.MAP_002.Crear(textoDeCelda ?? string.Empty);

            int? valorDeCampo = Entero(campo);
            int? valorDeTipo = Entero(tipo);
            // EsCompatible no comprueba que el campo esté definido: se repite aquí (Enum.IsDefined) antes de llamarla.
            bool campoDefinido = valorDeCampo.HasValue && Enum.IsDefined(typeof(CampoMapeable), valorDeCampo.Value);
            bool tipoDefinido = valorDeTipo.HasValue && Enum.IsDefined(typeof(TipoDeMapeo), valorDeTipo.Value);
            if (!campoDefinido || !tipoDefinido || !ValidadorDePublicacion.EsCompatible((CampoMapeable)valorDeCampo.Value, (TipoDeMapeo)valorDeTipo.Value))
                return CodigosDeError.MAP_003.Crear(EtiquetaParaElMensaje(valorDeCampo, campoDefinido));

            if ((TipoDeMapeo)valorDeTipo.Value == TipoDeMapeo.ColumnaDeBloqueDeCuotas)
            {
                int? filas = filasMaximas is int ? (int?)(int)filasMaximas : null;
                if (!filas.HasValue || filas.Value < 1 || filas.Value > GeneradorDeCalendario.PlazoMaximo) return CodigosDeError.MAP_004.Crear();
            }

            // La hoja NO se recorta: " X" es válida (spike 1, hallazgo 1).
            if (string.IsNullOrWhiteSpace(hoja as string)) return CodigosDeError.MAP_005.Crear();
            return null;
        }

        private static int? Entero(object valor)
        {
            var opcion = valor as ValorDeOpcion;
            if (opcion != null) return opcion.Valor;
            return valor is int ? (int?)(int)valor : null;
        }

        // La etiqueta con tildes y SIN comillas (EtiquetaDelCampo del validador las agrega y no sirve aquí), el número si no está definido o (vacío) si falta.
        private static string EtiquetaParaElMensaje(int? campo, bool definido)
        {
            if (!campo.HasValue) return "(vacío)";
            if (!definido) return campo.Value.ToString(CultureInfo.InvariantCulture);
            string etiqueta;
            return ValidadorDePublicacion.EtiquetasDeCampo.TryGetValue((CampoMapeable)campo.Value, out etiqueta) ? etiqueta : ((CampoMapeable)campo.Value).ToString();
        }

        // Identidad perezosa (0.3): se pregunta una sola vez y solo con una violación en la mano. El sistema pasa sin escribir (DC-03).
        private static ResultadoDeEvaluacion ResolverViolacion(ErrorDeNegocio violacion, IIdentidadDeEjecucion identidad)
        {
            return identidad.EsEscrituraDelSistema ? ResultadoDeEvaluacion.Permitir() : ResultadoDeEvaluacion.Rechazar(violacion);
        }

        private static ResultadoDeEvaluacion Escribiendo(object hoja, object celda)
        {
            string celdaEnMayuscula = ((string)celda).ToUpperInvariant();
            return ResultadoDeEvaluacion.Permitir(new[]
            {
                new KeyValuePair<string, object>(ColCelda, celdaEnMayuscula),
                new KeyValuePair<string, object>(ColNombre, NombreCalculado((string)hoja, celdaEnMayuscula))
            });
        }
    }
}
