using System;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// Los parámetros de entrada de <c>sanic_carp_capi_generarformato</c> (diseno/03 1.1), ya tipados, más el actor. Contenedor sin lógica. <c>null</c> en un Guid opcional significa
    /// "ausente" (también el <see cref="Guid.Empty"/>, que <c>ContextoDeApi.LeerGuid</c> ya convierte en <c>null</c>).
    /// </summary>
    internal sealed class PedidoDeGenerarFormato
    {
        /// <summary><c>arregloid</c>: el arreglo a imprimir. <c>null</c> ⇒ <c>CARP-GEN-001</c>.</summary>
        public Guid? ArregloId { get; }

        /// <summary><c>versionpreviaid</c>: si tiene valor, es VISTA PREVIA con esa versión (debe estar en Borrador): no congela y no escribe nada (DC-18).</summary>
        public Guid? VersionPreviaId { get; }

        /// <summary><c>InitiatingUserId</c> capturado antes de elevar (0.2): va a <c>sanic_generadoporid</c> y para <c>RetrievePrincipalAccess</c> de la vista previa. Nunca sale de <c>SharedVariables</c>.</summary>
        public Guid ActorId { get; }

        public PedidoDeGenerarFormato(Guid? arregloId, Guid? versionPreviaId, Guid actorId)
        {
            ArregloId = arregloId;
            VersionPreviaId = versionPreviaId;
            ActorId = actorId;
        }
    }

    /// <summary>Los parámetros de salida de <c>sanic_carp_capi_generarformato</c> (1.1). Se escriben TODOS siempre que la llamada termina bien (0.1). Contenedor sin lógica; los nulos se rechazan.</summary>
    internal sealed class ResultadoDeGenerarFormato
    {
        /// <summary><c>archivo</c>: el <c>.xlsx</c> en Base64 (<c>Convert.ToBase64String</c>, sin saltos de línea).</summary>
        public string Archivo { get; }

        /// <summary><c>nombrearchivo</c>: <c>F-NIC-0000052-</c> + la primaria del arreglo (<c>sanic_nombre</c>), más <c>.xlsx</c>. Sin datos del cliente.</summary>
        public string NombreDelArchivo { get; }

        /// <summary><c>numeroprovisional</c>: el número impreso para un Borrador; <c>0</c> si no aplica (Vigente, Terminado, Anulado, Descartado).</summary>
        public int NumeroProvisional { get; }

        /// <summary><c>congelado</c>: <c>true</c> si, al terminar, el arreglo está congelado (por esta llamada o antes). En vista previa es el estado que ya tenía. Un arreglo inactivo sin congelar da <c>false</c>.</summary>
        public bool Congelado { get; }

        /// <summary><c>advertencias</c>: los avisos del generador separados por <c>"\n"</c>; vacío (nunca <c>null</c>) si no hay.</summary>
        public string Advertencias { get; }

        public ResultadoDeGenerarFormato(string archivo, string nombreDelArchivo, int numeroProvisional, bool congelado, string advertencias)
        {
            if (archivo == null) throw new ArgumentNullException(nameof(archivo));
            if (nombreDelArchivo == null) throw new ArgumentNullException(nameof(nombreDelArchivo));
            if (advertencias == null) throw new ArgumentNullException(nameof(advertencias));
            Archivo = archivo;
            NombreDelArchivo = nombreDelArchivo;
            NumeroProvisional = numeroProvisional;
            Congelado = congelado;
            Advertencias = advertencias;
        }
    }

    /// <summary>
    /// Los cuatro datos del cliente que el formato imprime y que la congelación copia a las instantáneas (1.1 "Datos del generador" y paso 10a). Cualquiera puede ser <c>null</c> (un Cliente sin
    /// correo ni celular, PA-GEN-20); un texto vacío se normaliza a <c>null</c> (DC-01: Dataverse guarda el texto vacío como nulo). Contenedor sin lógica de lectura.
    /// </summary>
    internal sealed class DatosDelClienteParaElFormato
    {
        public string Nombre { get; }
        public string Cedula { get; }
        public string Correo { get; }
        public string Celular { get; }

        public DatosDelClienteParaElFormato(string nombre, string cedula, string correo, string celular)
        {
            Nombre = string.IsNullOrEmpty(nombre) ? null : nombre;
            Cedula = string.IsNullOrEmpty(cedula) ? null : cedula;
            Correo = string.IsNullOrEmpty(correo) ? null : correo;
            Celular = string.IsNullOrEmpty(celular) ? null : celular;
        }
    }
}
