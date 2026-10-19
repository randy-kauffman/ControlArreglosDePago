using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Sanic.Carp.Plugins.Dominio.Conversacion
{
    /// <summary>
    /// Las reglas de la Conversación (diseno/03 2.3 y 2.4, firma de 4.4; "Decisiones del arquitecto, 2026-10-19 (sublote 5b)"). Dominio puro: sin <c>Entity</c> ni SDK, sin estado
    /// (ningún campo mutable, ni estático: las listas son <see cref="ReadOnlyCollection{T}"/>), sin leer el reloj (entra en el pedido) y con la identidad perezosa (se consulta
    /// solo cuando la regla ya vio que hay algo que decidir). <b>STUB DEL ARQUITECTO</b>: <see cref="Evaluar"/> y <see cref="NombreTieneFormatoDeAutonumerico"/> lanzan
    /// <see cref="NotImplementedException"/>; las dos listas de columnas ya son datos.
    /// </summary>
    public static class ReglasDeConversacion
    {
        private static readonly IReadOnlyList<string> PreImagenDeActualizacion = new ReadOnlyCollection<string>(new[]
        {
            "statecode", "statuscode", "sanic_huellamessageid", "sanic_fechaprimercorreo", "sanic_asunto", "sanic_remitente", "sanic_sinrespuestadesde",
            "sanic_correonuevoenfinalizada", "sanic_resultado", "sanic_motivosinrespuesta", "sanic_detallesinrespuesta", "sanic_fechafinalizacion",
            "sanic_tiempoprimerarespuesta", "sanic_tiempototal", "sanic_cantidadrespuestas", "sanic_unidaaid", "sanic_finalizadoporid",
            "sanic_clienteid", "sanic_nombre", "sanic_oficialasignadoid", "ownerid"
        });

        private static readonly IReadOnlyList<string> PreImagenDeAsignacion = new ReadOnlyCollection<string>(new[] { "sanic_oficialasignadoid" });

        /// <summary>
        /// Las 21 columnas de la pre-imagen de S-17 (sección 5 y 2.3), en el orden de la sección 5. Es la unión de las columnas solo-sistema, las de gesto, <c>statecode</c>,
        /// <c>statuscode</c> y las tres que se vigilan en una conversación inactiva (<c>sanic_clienteid</c>, <c>sanic_oficialasignadoid</c>, <c>ownerid</c>). El step declara su
        /// registro con esta lista; una prueba la contrasta con la sección 5 y con las categorías de <see cref="Columnas"/>.
        /// </summary>
        public static IReadOnlyList<string> ColumnasDeLaPreImagenDeActualizacion { get { return PreImagenDeActualizacion; } }

        /// <summary>La pre-imagen de S-18: solo <c>sanic_oficialasignadoid</c> (sección 5 y 2.4).</summary>
        public static IReadOnlyList<string> ColumnasDeLaPreImagenDeAsignacion { get { return PreImagenDeAsignacion; } }

        /// <summary>
        /// ¿Cumple <paramref name="nombre"/> el formato del autonumérico de Conversación, <c>^CONV-[0-9]{6,15}$</c> (D-41, 2.3)? Comparación ORDINAL y sensible a mayúsculas, sin recortar espacios:
        /// <c>"CONV-000001"</c> sí; <c>null</c>, el texto vacío, <c>"conv-000001"</c>, <c>"CONV-12345"</c> (5 dígitos), 16 dígitos (no cabe en la columna de 20), un espacio al principio o al final,
        /// un salto de línea al final y cualquier carácter que no sea un dígito ASCII (un dígito de ancho completo o arábigo-índico) no. Nunca lanza. Hay que recorrer los caracteres a
        /// mano o con un patrón que acabe en <c>\z</c>: en .NET el <c>$</c> de una expresión regular acepta un salto de línea final, y <c>\d</c> acepta dígitos Unicode.
        /// </summary>
        public static bool NombreTieneFormatoDeAutonumerico(string nombre)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Evalúa UN guardado. Nunca devuelve <c>null</c> ni lanza por una regla de negocio (eso es <see cref="ResultadoDeEvaluacion.Rechazar"/>); lanza
        /// <see cref="ArgumentNullException"/> si <paramref name="pedido"/> es nulo y <see cref="ArgumentException"/> si la pre-imagen de una actualización no trae <c>statecode</c> y
        /// <c>statuscode</c>, o trae un par que no existe (tabla 0.7). El resultado tiene una pregunta de identidad solo donde se dice abajo: un test cuenta las consultas.
        /// <para>
        /// <b>Convenciones comunes.</b> (a) "Cambió" es DC-01: la clave está en el <c>Target</c> Y su valor difiere del de la pre-imagen; <c>""</c> y <c>null</c> son lo mismo. (b)
        /// <b>Una columna AUSENTE de la pre-imagen vale nulo</b> (Conversación no tiene seguridad por columna y la plataforma no devuelve las columnas nulas; sin esto, reenviar una
        /// fila completa con sus nulos se vería como cambios que no existen). Es la excepción deliberada a DC-01 punto 1 ("ausente no es nulo"), que sigue valiendo donde hay seguridad por
        /// columna (Arreglo, Cliente). Las claves de la pre-imagen que no son columnas del diseño no cuentan. (c) El <c>Target</c> también trae columnas de la plataforma
        /// (<c>modifiedon</c>, <c>modifiedby</c>, el id primario…): como no son columnas vigiladas, no cuentan nunca. (d) <c>EsEscrituraDelSistema</c> se consulta UNA vez como máximo y solo
        /// después de ver que hay una columna vigilada que cambió (0.3); para el sistema la regla termina permitiendo sin escribir nada (DC-03).
        /// </para>
        /// <para>
        /// <b>Alta</b> (S-36, 2.3): <c>sanic_nombre</c> ausente, nulo o vacío ⇒ permitir. Si cumple <see cref="NombreTieneFormatoDeAutonumerico"/> ⇒ permitir SIN consultar la identidad. Si no (un
        /// texto de espacios tampoco cumple, DC-01): consultar la identidad; sistema ⇒ permitir; persona ⇒ <c>CARP-CNV-001</c> nombrando <c>sanic_nombre</c>. Nunca escribe nada. Un valor que no es
        /// texto no cumple el formato.
        /// </para>
        /// <para>
        /// <b>Asignación</b> (S-18, 2.4): (1) <c>sanic_oficialasignadoid</c> igual al de la pre-imagen (por el id de la referencia) o ausente del <c>Target</c> ⇒ permitir. (2) Sistema ⇒ permitir.
        /// (3) Actual vacío y nuevo = <c>Identidad.ActorId</c> ("Tomar") ⇒ permitir SIN consultar <c>EsJefatura()</c>. (4) Cualquier otro cambio ⇒ <c>EsJefatura()</c> (UNA llamada): verdadero ⇒
        /// permitir, falso ⇒ <c>CARP-CNV-008</c>. Nunca escribe nada. No mira el estado de la conversación: la inactiva la rechaza antes S-17 (orden 10).
        /// </para>
        /// <para>
        /// <b>Actualización</b> (S-17, 2.3), en este orden y ganando el primero que rechaza:
        /// </para>
        /// <list type="number">
        /// <item><b>Qué cambió.</b> Las columnas solo-sistema (<c>Columnas.De(Conversacion).SoloSistema</c>, incluida <c>sanic_nombre</c>) y de gesto que cambiaron; si la pre-imagen es Finalizada o Unida,
        /// también <c>sanic_clienteid</c>, <c>sanic_oficialasignadoid</c> y <c>ownerid</c>. <b>El estado cambió</b> si el estado DESTINO difiere del de la pre-imagen. El destino (DC-06 y DC-29, ver más
        /// abajo) es: con <c>statecode</c> y <c>statuscode</c> en el <c>Target</c>, ese par; con solo <c>statecode</c>, ese estado con su motivo POR DEFECTO (<c>Estados.StatusPorDefecto</c>), aunque el
        /// <c>statecode</c> sea el mismo que el de la pre-imagen; con solo <c>statuscode</c>, el <c>statecode</c> de la pre-imagen con ese motivo (la plataforma valida el motivo contra el estado
        /// actual, RESULTADO.md (6)); con ninguno de los dos, el de la pre-imagen. Un <c>statecode</c>/<c>statuscode</c> presente con valor <c>null</c> cuenta como ausente. Si no cambió el estado y no
        /// cambió ninguna columna ⇒ permitir sin consultar la identidad.</item>
        /// <item>Sistema ⇒ permitir sin escribir nada (DC-03; también para un estado que la plataforma reescribiría, ver DC-29).</item>
        /// <item><b>Conversación inactiva.</b> Pre-imagen Finalizada o Unida y cambió alguna columna (cualquiera de las del paso 1, sin contar el estado) ⇒ <c>CARP-CNV-009</c>, salvo el gesto Reabrir (pre-imagen
        /// Finalizada y destino Abierta), que sigue al 3 bis.</item>
        /// <item>3 bis. <b>Reabrir es puro (D-40).</b> Cambió alguna de <c>sanic_clienteid</c>, <c>sanic_nombre</c>, <c>sanic_oficialasignadoid</c>, <c>ownerid</c> ⇒ <c>CARP-CNV-010</c> nombrando las que cambiaron,
        /// en ese orden y separadas por coma y espacio. Gana a los pasos 4 y 5.</item>
        /// <item>Cambió alguna solo-sistema ⇒ <c>CARP-CNV-001</c> nombrando las que cambiaron (en el orden de <c>Columnas</c>, separadas por coma y espacio).</item>
        /// <item><b>Estado.</b> Sea <c>de</c> el de la pre-imagen y <c>a</c> el destino. Si cambió el estado: Abierta→Finalizada es <b>Finalizar</b>; Finalizada→Abierta es <b>Reabrir</b>; cualquier destino
        /// Unida (el par Inactivo/Unida) ⇒ <c>CARP-CNV-006</c>; desde Unida a cualquier otro ⇒ <c>CARP-CNV-002</c>; cualquier otra combinación (también un par que no existe) ⇒ <c>CARP-CNV-002</c>. Orden: primero
        /// el destino Unida, luego el origen Unida. Finalizar exige <c>sanic_resultado</c> en el <c>Target</c> (ausente, nulo o sin valor) ⇒ <c>CARP-CNV-003</c>; si es SinRespuesta exige en el <c>Target</c>
        /// <c>sanic_motivosinrespuesta</c> y <c>sanic_detallesinrespuesta</c> no vacíos (el detalle recortado) ⇒ <c>CARP-CNV-004</c>. Reabrir con resultado, motivo o detalle en el <c>Target</c> y distinto
        /// del de la pre-imagen ⇒ <c>CARP-CNV-005</c>. Si el estado NO cambió y el resultado, el motivo o el detalle cambiaron ⇒ <c>CARP-CNV-005</c>.</item>
        /// <item><b>Efectos</b> (<see cref="ResultadoDeEvaluacion.Escribir"/>, valores neutros). <b>Finalizar</b>: <c>sanic_finalizadoporid</c> = <c>ReferenciaDeEntidad("systemuser", Identidad.ActorId)</c>;
        /// <c>sanic_fechafinalizacion</c> = <c>AhoraUtc</c>; <c>sanic_tiempototal</c> = <c>TiemposDeConversacion.MinutosEntre(fechaprimercorreo de la pre-imagen, AhoraUtc)</c> (no se escribe si
        /// <c>sanic_fechaprimercorreo</c> es nula); <c>sanic_sinrespuestadesde</c> = <c>null</c>; si el resultado NO es SinRespuesta, <c>sanic_motivosinrespuesta</c> y <c>sanic_detallesinrespuesta</c> = <c>null</c> (si lo es,
        /// no se tocan). <b>Reabrir</b>: <c>sanic_resultado</c>, <c>sanic_motivosinrespuesta</c>, <c>sanic_detallesinrespuesta</c>, <c>sanic_fechafinalizacion</c>, <c>sanic_finalizadoporid</c>,
        /// <c>sanic_tiempototal</c> = <c>null</c> y <c>sanic_correonuevoenfinalizada</c> = <c>false</c>; NO se toca <c>sanic_sinrespuestadesde</c> (DC-10). En ningún caso se escribe <c>statuscode</c>.
        /// Cualquier otra cosa permitida (sin cambio de estado, o el sistema) no escribe nada.</item>
        /// </list>
        /// <para>
        /// <b>DC-29 (decisión del sublote 5b).</b> Reenviar el MISMO <c>statecode</c> sin <c>statuscode</c> no es "no cambio" cuando el motivo de la pre-imagen no es el de por defecto de ese estado: la plataforma lo
        /// resetea. En Conversación solo ocurre en una <b>Unida</b> (Abierta y Finalizada ya tienen el motivo por defecto): <c>statecode = 1</c> la convertiría en Finalizada. Se evalúa como el gesto que la plataforma
        /// produciría (el destino del paso 1), así que una persona recibe <c>CARP-CNV-002</c> (Unida→cualquiera, 3.1); si además cambia otras columnas gana el paso 3, <c>CARP-CNV-009</c>. En una Abierta o una
        /// Finalizada el reenvío sigue sin ser un cambio. Con el par completo y válido DC-01 sigue igual. El sistema (DC-03) no queda protegido: ninguna escritura del diseño manda solo <c>statecode</c> (0.7).
        /// </para>
        /// </summary>
        public static ResultadoDeEvaluacion Evaluar(PedidoDeConversacion pedido)
        {
            throw new NotImplementedException();
        }
    }
}
