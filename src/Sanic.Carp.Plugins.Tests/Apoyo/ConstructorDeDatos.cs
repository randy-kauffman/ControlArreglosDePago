using System;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;

namespace Sanic.Carp.Plugins.Tests.Apoyo
{
    /// <summary>
    /// Armado de una fila de prueba (diseno/03 4.5, "ConstructorDeDatos"). Es un constructor fluido: parte de los valores por defecto
    /// del diccionario de la tabla y la prueba cambia solo lo que le importa. Cada <see cref="Construir"/> devuelve una
    /// <see cref="Entity"/> NUEVA e independiente (mismo <see cref="Id"/>).
    /// </summary>
    internal sealed class ConstructorDeFila
    {
        internal ConstructorDeFila(string tabla) { throw new NotImplementedException(); }

        public string Tabla { get { throw new NotImplementedException(); } }

        /// <summary>Id de la fila que se construye (fijo por constructor; se cambia con <see cref="ConId"/>).</summary>
        public Guid Id { get { throw new NotImplementedException(); } }

        /// <summary>Fija el id (también el atributo del id primario).</summary>
        public ConstructorDeFila ConId(Guid id) { throw new NotImplementedException(); }

        /// <summary>Pone o reemplaza una columna con el valor TAL CUAL (sin convertirlo). Un valor <c>null</c> equivale a <see cref="Sin"/>.</summary>
        public ConstructorDeFila Con(string columna, object valor) { throw new NotImplementedException(); }

        /// <summary>Quita la columna: la fila queda con la columna AUSENTE (no en nulo).</summary>
        public ConstructorDeFila Sin(string columna) { throw new NotImplementedException(); }

        /// <summary>La fila. Lleva su id primario como atributo <see cref="Guid"/> y <see cref="Entity.Id"/>; sin <c>RowVersion</c>.</summary>
        public Entity Construir() { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Constructores de filas con los valores por defecto del diccionario de datos (<c>diseno/02-diccionario-datos.md</c>) y del
    /// estado que se pida (diseno/03 4.5). Reglas que fijan <c>ConstructorDeDatosAceptacion</c> y <c>DobleContraRespuestasRealesAceptacion</c>:
    /// <list type="bullet">
    /// <item>Cada fila trae TODAS las columnas requeridas (<c>Req = Sí</c>) de su tabla en el diccionario, con el tipo SDK que la plataforma
    /// devuelve para esa columna (fixtures de Dev): choice ⇒ <c>OptionSetValue</c>, lookup ⇒ <c>EntityReference</c> a la tabla destino SIN
    /// <c>Name</c>, <c>sino</c> ⇒ <c>bool</c>, entero ⇒ <c>int</c>, decimal ⇒ <c>decimal</c> (nunca <c>Money</c>), fechahora UL ⇒ <c>DateTime</c>
    /// <c>Kind=Utc</c>, fecha DO ⇒ <c>DateTime</c> <c>Kind=Unspecified</c> a las 00:00.</item>
    /// <item>Ninguna columna que no exista en el diccionario (salvo el id primario, <c>ownerid</c>, <c>statecode</c> y <c>statuscode</c>) ni ninguna
    /// de fórmula o de archivo.</item>
    /// <item>Los choices y los estados se arman con <see cref="Estados"/> y los enums de <c>Choices.g.cs</c>, nunca con literales.</item>
    /// <item>Nada de datos reales (def. L411): textos con "PRUEBA" o "zzf", correos en <c>example.com</c>. Los datos que son clave de negocio (cédula, número de producto,
    /// huella, <c>messageid</c>, nombre de cuota) y los Guid de los lookups son DISTINTOS en cada fila construida.</item>
    /// <item>Cada fila es un SUBCONJUNTO de la fila real del mismo estado (<c>DobleContraRespuestasRealesAceptacion</c>, PA-DOB-14): una columna opcional que la fila real no
    /// tiene (por ejemplo <c>sanic_huella</c> en una versión, <c>senton</c> en un entrante) no se agrega por defecto; la prueba que la necesita usa <c>Con</c>. Cada
    /// <see cref="ConstructorDeFila.Construir"/> devuelve objetos nuevos (también los <c>OptionSetValue</c> y <c>EntityReference</c>).</item>
    /// </list>
    /// </summary>
    internal static class ConstructorDeDatos
    {
        public static ConstructorDeFila UnaConversacion(EstadoDeConversacion estado = EstadoDeConversacion.Abierta) { throw new NotImplementedException(); }

        public static ConstructorDeFila UnArreglo(EstadoDeArreglo estado = EstadoDeArreglo.Borrador) { throw new NotImplementedException(); }

        public static ConstructorDeFila UnaCuota() { throw new NotImplementedException(); }

        /// <summary>Email entrante recibido: <c>directioncode</c> false, <c>statecode</c> 1 / <c>statuscode</c> 4, sin <c>senton</c> (fixture 01).</summary>
        public static ConstructorDeFila UnCorreoEntrante() { throw new NotImplementedException(); }

        /// <summary>Email saliente enviado: <c>directioncode</c> true, <c>statecode</c> 1 / <c>statuscode</c> 3, con <c>senton</c> (fixture 01).</summary>
        public static ConstructorDeFila UnCorreoSaliente() { throw new NotImplementedException(); }

        public static ConstructorDeFila UnaVersion(EstadoDeVersionDePlantilla estado = EstadoDeVersionDePlantilla.Borrador) { throw new NotImplementedException(); }

        public static ConstructorDeFila UnMapeo() { throw new NotImplementedException(); }

        // ---- Fuera de la lista de 4.5, pero necesarios para las tablas de los fixtures 4 a 6 (D-59 vive en teammembership) ----

        public static ConstructorDeFila UnCliente() { throw new NotImplementedException(); }

        /// <summary><c>teammembership</c>: exactamente <c>teammembershipid</c>, <c>systemuserid</c> y <c>teamid</c>, los tres <see cref="Guid"/> CRUDOS (fixture 04).</summary>
        public static ConstructorDeFila UnaMembresiaDeTeam(Guid usuario, Guid team) { throw new NotImplementedException(); }

        public static ConstructorDeFila UnTeam(string nombre, Guid unidadDeNegocio, bool predeterminado) { throw new NotImplementedException(); }

        public static ConstructorDeFila UnaCola(string correo) { throw new NotImplementedException(); }

        /// <summary>Una persona: <c>fullname</c>, <c>isdisabled</c> false, <c>accessmode</c> 0, <c>businessunitid</c> (fixture 06).</summary>
        public static ConstructorDeFila UnUsuario(Guid id) { throw new NotImplementedException(); }

        /// <summary>SYSTEM: <c>fullname</c> "SYSTEM", <c>isdisabled</c> TRUE, <c>accessmode</c> 0 (fixture 06; una regla "usuario habilitado" no aplica a SYSTEM).</summary>
        public static ConstructorDeFila UnSystem(Guid id) { throw new NotImplementedException(); }

        /// <summary><c>environmentvariabledefinition</c> de tipo texto (<c>type</c> 100000000) con su valor por defecto.</summary>
        public static ConstructorDeFila UnaDefinicionDeVariable(string nombreDeEsquema, string valorPorDefecto) { throw new NotImplementedException(); }

        /// <summary><c>environmentvariablevalue</c> de una definición; <c>environmentvariabledefinitionid</c> es <c>EntityReference</c>.</summary>
        public static ConstructorDeFila UnValorDeVariable(string nombreDeEsquema, Guid definicion, string valor) { throw new NotImplementedException(); }
    }
}
