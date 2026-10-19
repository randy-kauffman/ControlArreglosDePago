using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio.Errores;

namespace Sanic.Carp.Plugins.Dominio.Plantilla
{
    /// <summary>
    /// Las reglas de la Versión de plantilla (diseno/03 2.8, máquina 3.3, firmas de 4.4; "Decisiones del arquitecto, 2026-10-19 (sublote 5e)"). Dominio puro: sin <c>Entity</c> ni SDK,
    /// sin estado (las listas son <see cref="ReadOnlyCollection{T}"/>), sin leer el reloj y con la identidad perezosa (0.3). Usa <c>Cambios</c>, <c>CodigosDeError</c> y
    /// <c>ResultadoDeEvaluacion</c> sin reimplementarlos. DC-29 no aplica: la Versión tiene los dos motivos por defecto (1 Activo, 2 Inactivo), así que reenviar el mismo
    /// <c>statecode</c> o <c>statuscode</c> no cambia nada y es "no cambió" (DC-01).
    /// </summary>
    public static class ReglasDeVersion
    {
        private static readonly IReadOnlyList<string> PreImagen = new ReadOnlyCollection<string>(new[]
        {
            "statecode", "statuscode", "sanic_nombre", "sanic_estado", "sanic_capacidad", "sanic_huella", "sanic_fechaaprobacion", "sanic_aprobadoporid"
        });

        /// <summary>
        /// Las 8 columnas de la pre-imagen <c>PreImagen</c> de S-26 (seccion 5), en ese orden. Son las columnas VIGILADAS de la regla: lo que no está aquí (<c>sanic_archivo</c>, que rige S-27;
        /// <c>ownerid</c>, que rige S-12; <c>modifiedon</c> y el id primario) no cuenta como cambio.
        /// </summary>
        public static IReadOnlyList<string> ColumnasDeLaPreImagen { get { return PreImagen; } }

        /// <summary>
        /// ¿El número de versión cumple <c>^v[0-9]{2,3}$</c>? Ordinal, sensible a mayúsculas, sin recortar espacios y SIN aceptar un salto de línea final (el <c>$</c> de .NET lo acepta; esta función
        /// no): <c>"v07"</c> y <c>"v100"</c> sí; <c>"V07"</c>, <c>"v7"</c>, <c>"v1000"</c>, <c>"v07\n"</c>, <c>" v07"</c>, <c>""</c> y <c>null</c> no. Solo dígitos ASCII (<c>'0'</c> a <c>'9'</c>).
        /// </summary>
        public static bool NombreTieneFormatoDeVersion(string nombre)
        {
            // Sin Regex: el $ de .NET acepta un salto de linea final y char.IsDigit acepta otros alfabetos (diseno/03 2.8, CARP-VER-012).
            if (nombre == null || nombre.Length < 3 || nombre.Length > 4 || nombre[0] != 'v') return false;
            for (var i = 1; i < nombre.Length; i++)
                if (nombre[i] < '0' || nombre[i] > '9') return false;
            return true;
        }

        private const string ColEstadoNativo = "statecode";
        private const string ColMotivoNativo = "statuscode";
        private const string ColNombre = "sanic_nombre";
        private const string ColEstado = "sanic_estado";
        private const string ColCapacidad = "sanic_capacidad";
        private const string ColHuella = "sanic_huella";
        private const string ColFechaAprobacion = "sanic_fechaaprobacion";
        private const string ColAprobadoPor = "sanic_aprobadoporid";
        private const string ColArchivo = "sanic_archivo";

        // El choice llega como ValorDeOpcion (Target neutro) o como int; cualquier otra cosa (null incluido) no es una opcion.
        private static int? Opcion(object valor)
        {
            var opcion = valor as ValorDeOpcion;
            if (opcion != null) return opcion.Valor;
            return valor is int ? (int?)(int)valor : null;
        }

        private static int Borrador { get { return (int)EstadoDeVersionDePlantilla.Borrador; } }

        // Presente = la clave viene con valor no nulo (en un Create, null es "sin valor"). La huella vacia tampoco cuenta.
        private static bool Presente(IReadOnlyDictionary<string, object> target, string columna)
        {
            object valor;
            if (!target.TryGetValue(columna, out valor) || valor == null) return false;
            var texto = valor as string;
            return texto == null || texto.Length > 0;
        }

        private static ErrorDeNegocio ErrorDelAlta(PedidoDeAltaDeVersion pedido)
        {
            object estado;
            if (pedido.Target.TryGetValue(ColEstado, out estado) && estado != null)
            {
                var opcion = Opcion(estado);
                if (!opcion.HasValue || opcion.Value != Borrador) return CodigosDeError.VER_001.Crear();
            }
            if (Presente(pedido.Target, ColHuella) || Presente(pedido.Target, ColFechaAprobacion) || Presente(pedido.Target, ColAprobadoPor)) return CodigosDeError.VER_002.Crear();
            object nombre;
            pedido.Target.TryGetValue(ColNombre, out nombre);
            if (!NombreTieneFormatoDeVersion(nombre as string)) return CodigosDeError.VER_012.Crear();
            return null;
        }

        /// <summary>
        /// S-25 (2.8, <c>VersionPlantillaCrearStep</c>). Para una PERSONA (DC-03: el sistema pasa), en este orden y gana el primero:
        /// <list type="number">
        /// <item><c>sanic_estado</c> presente (clave en el <c>Target</c> con valor no nulo) y distinto de Borrador ⇒ <c>CARP-VER-001</c>. Ausente o <c>null</c> pasa (una versión nace siempre en Borrador, DC-19).</item>
        /// <item><c>sanic_huella</c> (texto no vacío), <c>sanic_fechaaprobacion</c> o <c>sanic_aprobadoporid</c> presentes (valor no nulo) ⇒ <c>CARP-VER-002</c>.</item>
        /// <item><c>sanic_nombre</c> ausente, nulo, vacío o que no cumple <see cref="NombreTieneFormatoDeVersion"/> ⇒ <c>CARP-VER-012</c> (la unicidad la da K-03, no esta regla).</item>
        /// </list>
        /// La identidad se pregunta UNA vez y solo si hay algo que rechazar (camino barato): <c>EsEscrituraDelSistema</c> ⇒ permite sin tocar el <c>Target</c>. Sin nada que rechazar permite sin consultar
        /// la identidad y, si <c>sanic_estado</c> no venía (ausente o <c>null</c>), lo ESCRIBE como Borrador (<c>ValorDeOpcion</c>) en <c>Escribir</c>: el diseño dice "siempre nace en Borrador" y no
        /// depende de que la plataforma complete el valor por defecto de la columna. <c>pedido</c> nulo ⇒ <see cref="ArgumentNullException"/>.
        /// </summary>
        public static ResultadoDeEvaluacion EvaluarAlta(PedidoDeAltaDeVersion pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));

            var error = ErrorDelAlta(pedido);
            if (error != null)
                return pedido.Identidad.EsEscrituraDelSistema ? ResultadoDeEvaluacion.Permitir() : ResultadoDeEvaluacion.Rechazar(error);

            // DC-19: una version nace siempre en Borrador, sin depender del valor por defecto de la columna.
            object estado;
            if (pedido.Target.TryGetValue(ColEstado, out estado) && estado != null) return ResultadoDeEvaluacion.Permitir();
            return ResultadoDeEvaluacion.Permitir(new[] { new KeyValuePair<string, object>(ColEstado, new ValorDeOpcion(Borrador)) });
        }

        /// <summary>
        /// S-26 (2.8 y 3.3, <c>VersionPlantillaActualizarStep</c>), sin la parte de Publicar que lee y genera (eso es <see cref="PublicacionDeVersion.Publicar"/>). Nunca escribe en el <c>Target</c>.
        /// En este orden:
        /// <list type="number">
        /// <item><b>Columnas cambiadas</b> entre las 8 de <see cref="ColumnasDeLaPreImagen"/> (DC-01: presente en el <c>Target</c> Y distinto de la pre-imagen; una clave ausente de la pre-imagen es desconocida y
        /// cuenta como cambio; <c>""</c> es <c>null</c>; <c>sanic_fechaaprobacion</c> es fecha y hora de usuario local, se compara por ticks con <c>Cambios</c>, sin <c>.Date</c>). Ninguna ⇒ permite SIN consultar la identidad.</item>
        /// <item><c>Identidad.EsEscrituraDelSistema</c> (una sola consulta) ⇒ permite sin publicar (DC-03; así pasa Vigente → Histórica).</item>
        /// <item>Estado anterior = <c>sanic_estado</c> de la pre-imagen; <c>null</c>, ausente o no definido es "no Borrador" (falla cerrado). Vigente, Histórica o desconocido: cualquier cambio ⇒ <c>CARP-VER-003</c>
        /// (también <c>statecode</c>/<c>statuscode</c> y un intento de volver a Borrador).</item>
        /// <item>Borrador, en este orden: (a) cambió <c>statecode</c> o <c>statuscode</c> ⇒ <c>CARP-VER-014</c>; (b) cambió <c>sanic_huella</c>, <c>sanic_fechaaprobacion</c> o <c>sanic_aprobadoporid</c> ⇒ <c>CARP-VER-002</c>;
        /// (c) cambió <c>sanic_estado</c>: si el nuevo valor no es Vigente (Histórica, nulo o no definido) ⇒ <c>CARP-VER-004</c>; si es Vigente (Publicar) y cambió <c>sanic_nombre</c> o <c>sanic_capacidad</c>, o el <c>Target</c>
        /// trae la clave <c>sanic_archivo</c> (con cualquier valor: un archivo cambiado en el mismo guardado esquivaría la huella), ⇒ <c>CARP-VER-013</c>; si no, devuelve <see cref="DecisionDeVersion.Publicar"/>;
        /// (d) el estado no cambió y cambió <c>sanic_nombre</c> a un valor que no cumple <see cref="NombreTieneFormatoDeVersion"/> ⇒ <c>CARP-VER-012</c>; si no, permite (edición libre de nombre, capacidad y archivo).</item>
        /// </list>
        /// El <c>sanic_estado</c> del <c>Target</c> puede venir como <c>ValorDeOpcion</c> o como <c>int</c>. <c>pedido</c> nulo ⇒ <see cref="ArgumentNullException"/>.
        /// </summary>
        public static DecisionDeVersion EvaluarActualizacion(PedidoDeVersion pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));

            // Paso 1: columnas cambiadas entre las 8. Ninguna => permite sin preguntar la identidad.
            var cambiadas = new HashSet<string>(StringComparer.Ordinal);
            foreach (var columna in PreImagen)
                if (CambioLaColumna(pedido, columna)) cambiadas.Add(columna);
            if (cambiadas.Count == 0) return DecisionDeVersion.Permitir();

            // Paso 2: DC-03, una sola pregunta.
            if (pedido.Identidad.EsEscrituraDelSistema) return DecisionDeVersion.Permitir();

            // Paso 3: el estado anterior; nulo, ausente o no definido es "no Borrador" (falla cerrado).
            object previo;
            pedido.PreImagen.TryGetValue(ColEstado, out previo);
            var anterior = Opcion(previo);
            if (!anterior.HasValue || anterior.Value != Borrador) return Rechazo(CodigosDeError.VER_003);

            // Paso 4: Borrador.
            if (cambiadas.Contains(ColEstadoNativo) || cambiadas.Contains(ColMotivoNativo)) return Rechazo(CodigosDeError.VER_014);
            if (cambiadas.Contains(ColHuella) || cambiadas.Contains(ColFechaAprobacion) || cambiadas.Contains(ColAprobadoPor)) return Rechazo(CodigosDeError.VER_002);

            if (cambiadas.Contains(ColEstado))
            {
                object nuevo;
                pedido.Target.TryGetValue(ColEstado, out nuevo);
                var destino = Opcion(nuevo);
                if (!destino.HasValue || destino.Value != (int)EstadoDeVersionDePlantilla.Vigente) return Rechazo(CodigosDeError.VER_004);
                // Publicar no admite otros cambios; un archivo en el mismo guardado esquivaria la huella (2.8, CARP-VER-013).
                if (cambiadas.Contains(ColNombre) || cambiadas.Contains(ColCapacidad) || pedido.Target.ContainsKey(ColArchivo)) return Rechazo(CodigosDeError.VER_013);
                return DecisionDeVersion.Publicar();
            }

            if (cambiadas.Contains(ColNombre))
            {
                object nombre;
                pedido.Target.TryGetValue(ColNombre, out nombre);
                if (!NombreTieneFormatoDeVersion(nombre as string)) return Rechazo(CodigosDeError.VER_012);
            }
            return DecisionDeVersion.Permitir();
        }

        private static DecisionDeVersion Rechazo(DefinicionDeError definicion)
        {
            return DecisionDeVersion.Rechazar(ResultadoDeEvaluacion.Rechazar(definicion.Crear()));
        }

        // DC-01 puro (Cambios.Distinto); la fecha de aprobacion es fecha y hora y Cambios la compara por ticks, sin .Date.
        private static bool CambioLaColumna(PedidoDeVersion pedido, string columna)
        {
            return Cambios.Distinto(pedido.Target, pedido.PreImagen, columna);
        }

        /// <summary>
        /// S-27 (2.8, <c>ArchivoDePlantillaStep</c>). El filtro del step garantiza que el <c>Target</c> trae <c>sanic_archivo</c>; la regla no mira su valor ni el mensaje padre ni la profundidad (V-1, V-4):
        /// <list type="number">
        /// <item><c>Identidad.EsEscrituraDelSistema</c> (UNA consulta; es <c>InitiatingUserId</c> y no <c>UserId</c>: la primera pasada del reemplazo con PATCH directo trae <c>UserId</c> = SYSTEM e <c>InitiatingUserId</c> = la persona y se evalúa como persona) ⇒ permite sin leer nada (DC-03).</item>
        /// <item>Si no: <c>Lecturas.EstadoDeLaVersion</c> (UNA lectura con SYSTEM); distinto de Borrador, o <c>null</c> (falla cerrado) ⇒ <c>CARP-VER-003</c>. Borrador ⇒ permite.</item>
        /// </list>
        /// Nunca escribe. <c>pedido</c> nulo ⇒ <see cref="ArgumentNullException"/>.
        /// </summary>
        public static ResultadoDeEvaluacion EvaluarArchivo(PedidoDeArchivo pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));

            if (pedido.Identidad.EsEscrituraDelSistema) return ResultadoDeEvaluacion.Permitir();
            var estado = pedido.Lecturas.EstadoDeLaVersion(pedido.VersionId);
            if (!estado.HasValue || estado.Value != EstadoDeVersionDePlantilla.Borrador) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.VER_003.Crear());
            return ResultadoDeEvaluacion.Permitir();
        }

        /// <summary>
        /// Convierte la lista de errores de <c>ValidadorDePublicacion.Validar</c> en UN error para lanzar (2.8, Publicar, paso 2: "con todos"). Lista nula o vacía ⇒ <see cref="ArgumentException"/>; un solo
        /// error ⇒ ESE error (la misma instancia); varios ⇒ un <c>ErrorDeNegocio</c> con el código del PRIMERO (el validador emite los <c>CARP-VER-007</c> antes que el <c>CARP-VER-008</c>) y el mensaje de cada
        /// error (<c>Mensaje</c>, sin el código) unidos por un salto de línea <c>"\n"</c>, en el orden de la lista. Un elemento nulo ⇒ <see cref="ArgumentException"/>.
        /// </summary>
        public static ErrorDeNegocio ErrorDeLaPublicacion(IReadOnlyList<ErrorDeNegocio> errores)
        {
            if (errores == null || errores.Count == 0) throw new ArgumentException("Debe haber al menos un error.", nameof(errores));
            foreach (var error in errores)
                if (error == null) throw new ArgumentException("Hay un error nulo en la lista.", nameof(errores));
            if (errores.Count == 1) return errores[0];

            var mensajes = new List<string>(errores.Count);
            foreach (var error in errores) mensajes.Add(error.Mensaje);
            return new ErrorDeNegocio(errores[0].Codigo, string.Join("\n", mensajes));
        }

        /// <summary>
        /// Lo que S-26 escribe en cada versión Vigente anterior al publicar (2.8, Publicar, paso 5): exactamente <c>sanic_estado</c> = Histórica (<c>ValorDeOpcion</c> de <c>EstadoDeVersionDePlantilla.Historica</c>),
        /// de solo lectura de verdad. No toca <c>statecode</c> (la retirada es por <c>sanic_estado</c>, D-40). Cada llamada devuelve un diccionario nuevo.
        /// </summary>
        public static IReadOnlyDictionary<string, object> ColumnasAlPasarAHistorica()
        {
            var columnas = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ColEstado, new ValorDeOpcion((int)EstadoDeVersionDePlantilla.Historica) }
            };
            return new ReadOnlyDictionary<string, object>(columnas);
        }

        /// <summary>
        /// Después de pasar a Histórica las otras Vigentes, <paramref name="otrasVigentes"/> es lo que la consulta <c>OtrasVigentes</c> sigue encontrando: cero ⇒ <c>null</c> (todo bien); uno o más ⇒
        /// <c>CARP-VER-010</c> (revierte todo, PA-VER-14). Un número negativo ⇒ <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        public static ErrorDeNegocio ErrorSiQuedaOtraVigente(int otrasVigentes)
        {
            if (otrasVigentes < 0) throw new ArgumentOutOfRangeException(nameof(otrasVigentes), otrasVigentes, "Un conteo no puede ser negativo.");
            return otrasVigentes == 0 ? null : CodigosDeError.VER_010.Crear();
        }
    }
}
