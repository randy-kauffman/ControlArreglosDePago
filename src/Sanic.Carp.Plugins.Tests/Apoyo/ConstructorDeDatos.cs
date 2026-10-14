using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Dobles;

namespace Sanic.Carp.Plugins.Tests.Apoyo
{
    /// <summary>
    /// Armado de una fila de prueba (diseno/03 4.5, "ConstructorDeDatos"). Es un constructor fluido: parte de los valores por defecto
    /// del diccionario de la tabla y la prueba cambia solo lo que le importa. Cada <see cref="Construir"/> devuelve una
    /// <see cref="Entity"/> NUEVA e independiente (mismo <see cref="Id"/>).
    /// </summary>
    internal sealed class ConstructorDeFila
    {
        private readonly string _tabla;
        private readonly List<KeyValuePair<string, object>> _columnas = new List<KeyValuePair<string, object>>();
        private Guid _id = Guid.NewGuid();

        internal ConstructorDeFila(string tabla)
        {
            if (string.IsNullOrEmpty(tabla)) throw new ArgumentException("Falta la tabla.", nameof(tabla));
            _tabla = tabla;
        }

        public string Tabla { get { return _tabla; } }

        /// <summary>Id de la fila que se construye (fijo por constructor; se cambia con <see cref="ConId"/>).</summary>
        public Guid Id { get { return _id; } }

        /// <summary>Fija el id (también el atributo del id primario).</summary>
        public ConstructorDeFila ConId(Guid id)
        {
            _id = id;
            return this;
        }

        /// <summary>Pone o reemplaza una columna con el valor TAL CUAL (sin convertirlo). Un valor <c>null</c> equivale a <see cref="Sin"/>.</summary>
        public ConstructorDeFila Con(string columna, object valor)
        {
            if (string.IsNullOrEmpty(columna)) throw new ArgumentException("Falta la columna.", nameof(columna));
            Quitar(columna);
            if (valor != null) _columnas.Add(new KeyValuePair<string, object>(columna, valor));
            return this;
        }

        /// <summary>Quita la columna: la fila queda con la columna AUSENTE (no en nulo).</summary>
        public ConstructorDeFila Sin(string columna)
        {
            Quitar(columna);
            return this;
        }

        private void Quitar(string columna)
        {
            _columnas.RemoveAll(par => string.Equals(par.Key, columna, StringComparison.Ordinal));
        }

        /// <summary>La fila. Lleva su id primario como atributo <see cref="Guid"/> y <see cref="Entity.Id"/>; sin <c>RowVersion</c>.</summary>
        public Entity Construir()
        {
            var fila = new Entity(_tabla, _id);
            foreach (var par in _columnas) fila.Attributes[par.Key] = CopiarValor(par.Value);
            // El id primario va como atributo Guid, igual al Id de la entidad; sin RowVersion (lo asigna el almacén).
            fila.Attributes[FormaDeLaPlataforma.ColumnaDeId(_tabla)] = _id;
            return fila;
        }

        private static object CopiarValor(object valor)
        {
            return Copias.Valor(valor);
        }
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
        /// <summary>
        /// <c>systemuser.accessmode</c> 0 (Read-Write, fixture 06). Sin esta constante el número iría suelto en el código del constructor (conv. §6).
        /// </summary>
        private const int ModoDeAccesoNormal = 0;

        /// <summary>
        /// <c>environmentvariabledefinition.type</c> de tipo texto (fixture 05). Con separador de dígitos para que el nombre y el número queden juntos y claros.
        /// </summary>
        private const int TipoDeVariableTexto = 100_000_000;

        /// <summary>
        /// Un número que distingue a la fila que se construye de cualquier otra (las claves de negocio: cédula, número de producto, huella, <c>messageid</c>, nombre de cuota). Sale de un <see cref="Guid"/> nuevo
        /// (48 bits al azar, 12 dígitos), no de un contador de proceso: ningún estado del constructor sobrevive a la fila, así que el valor de una clave no depende de cuántas filas construyó
        /// otra prueba antes ni de cuántos hilos construyen a la vez (diseno/03 4.5, vuelta 2, hallazgo 8; <c>PA_STA_05</c>).
        /// </summary>
        private static long Siguiente()
        {
            var bytes = Guid.NewGuid().ToByteArray();
            var cuarentaYOchoBits = ((ulong)BitConverter.ToUInt32(bytes, 0) << 16) | BitConverter.ToUInt16(bytes, 4);
            return (long)(cuarentaYOchoBits % 1000000000000UL);
        }

        /// <summary>Un decimal por defecto como lo devuelve la plataforma: escala 10 (<c>1000.50m</c> vuelve <c>1000.5000000000m</c>). <c>Con</c> pasa el valor tal cual (PA_CDD_15).</summary>
        private static decimal Dec(decimal valor)
        {
            return FormaDeLaPlataforma.Escala10(valor);
        }

        /// <summary>El estado Activo con su motivo por defecto: el de las tablas estándar y el de un alta (P-13).</summary>
        private static ConstructorDeFila ConEstadoActivo(ConstructorDeFila constructor)
        {
            return constructor.Con("statecode", new OptionSetValue(Estados.Activo)).Con("statuscode", new OptionSetValue(Estados.StatusPorDefecto(Estados.Activo)));
        }

        /// <summary>Instante fijo de los datos construidos: el del <c>RelojFijo</c>. Los constructores nunca leen el reloj del sistema.</summary>
        private static DateTime Ahora
        {
            get { return RelojFijo.InstanteInicial; }
        }

        private static DateTime SoloFecha(int anio, int mes, int dia)
        {
            return new DateTime(anio, mes, dia, 0, 0, 0, DateTimeKind.Unspecified);
        }

        private static EntityReference Ref(string tabla)
        {
            return new EntityReference(tabla, Guid.NewGuid());
        }

        private static ConstructorDeFila ConPropietario(ConstructorDeFila constructor)
        {
            // El team propietario del área (D-31): el mismo que ConfiguracionSimulada, para que "el propietario es el team del área" se pueda afirmar.
            return constructor.Con("ownerid", new EntityReference("team", ConfiguracionSimulada.IdDeTeamPropietarioPorDefecto));
        }

        private static ConstructorDeFila ConEstado(ConstructorDeFila constructor, EstadoEfectivo estado)
        {
            return constructor.Con("statecode", new OptionSetValue(estado.StateCode)).Con("statuscode", new OptionSetValue(estado.StatusCode));
        }

        public static ConstructorDeFila UnaConversacion(EstadoDeConversacion estado = EstadoDeConversacion.Abierta)
        {
            var n = Siguiente();
            var constructor = ConPropietario(new ConstructorDeFila("sanic_carp_tbl_conversacion"))
                .Con("sanic_nombre", "CONV-" + n.ToString("D6", CultureInfo.InvariantCulture))
                .Con("sanic_huellamessageid", Huella("conversacion-" + n.ToString(CultureInfo.InvariantCulture)))
                .Con("sanic_fechaprimercorreo", Ahora.AddDays(-2))
                .Con("sanic_asunto", "ZZF-PRUEBA asunto " + n.ToString(CultureInfo.InvariantCulture))
                .Con("sanic_remitente", "zzf-remitente-" + n.ToString(CultureInfo.InvariantCulture) + "@example.com")
                .Con("sanic_correonuevoenfinalizada", false)
                .Con("sanic_cantidadrespuestas", 0);
            ConEstado(constructor, Estados.De(estado));
            if (estado != EstadoDeConversacion.Abierta) constructor.Con("sanic_fechafinalizacion", Ahora.AddDays(-1));
            if (estado == EstadoDeConversacion.Finalizada)
            {
                constructor.Con("sanic_resultado", new OptionSetValue((int)ResultadoDeConversacion.SinRespuesta))
                    .Con("sanic_motivosinrespuesta", new OptionSetValue((int)MotivoDeSinRespuesta.Otro))
                    .Con("sanic_detallesinrespuesta", "ZZF-PRUEBA detalle de sin respuesta")
                    .Con("sanic_finalizadoporid", Ref("systemuser"));
            }
            if (estado == EstadoDeConversacion.Unida) constructor.Con("sanic_unidaaid", Ref("sanic_carp_tbl_conversacion"));
            return constructor;
        }

        /// <summary>SHA-256 en hexadecimal minúscula, calculado con la BCL (nunca con el código bajo prueba).</summary>
        private static string Huella(string texto)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(texto));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        public static ConstructorDeFila UnArreglo(EstadoDeArreglo estado = EstadoDeArreglo.Borrador)
        {
            var n = Siguiente();
            var texto = n.ToString("D6", CultureInfo.InvariantCulture);
            // Montos coherentes con la regla de diseno/03 2.0.3: saldo remanente = saldo inicial - abonado; total = el menor entre cuota x plazo y el remanente.
            var constructor = ConPropietario(new ConstructorDeFila("sanic_carp_tbl_arreglo"))
                .Con("sanic_nombre", "ARR-" + texto)
                .Con("sanic_producto", new OptionSetValue((int)ProductoCrediticio.Prestamo))
                .Con("sanic_numeroproducto", "ZZF-PRUEBA-" + texto)
                .Con("sanic_tipo", new OptionSetValue((int)TipoDeArreglo.Temporal))
                .Con("sanic_moneda", new OptionSetValue((int)Moneda.Cordobas))
                .Con("sanic_canalorigen", new OptionSetValue((int)CanalDeOrigen.Correo))
                .Con("sanic_saldoinicial", Dec(1000.50m))
                .Con("sanic_montoabonado", Dec(100.50m))
                .Con("sanic_saldoremanente", Dec(900.00m))
                .Con("sanic_plazo", 6)
                .Con("sanic_cuotamensual", Dec(150.00m))
                .Con("sanic_totalpagar", Dec(900.00m))
                .Con("sanic_fechainicio", SoloFecha(2026, 11, 1))
                .Con("sanic_fechafin", SoloFecha(2027, 4, 1))
                .Con("sanic_cuentadebitar", "ZZF-cta-" + texto)
                .Con("sanic_fechaformalizacion", SoloFecha(2026, 10, 10))
                .Con("sanic_cantidadreaperturas", 0)
                .Con("sanic_clienteid", Ref("sanic_carp_tbl_cliente"))
                .Con("sanic_versionplantillaid", Ref("sanic_carp_tbl_versionplantilla"))
                .Con("sanic_oficialid", Ref("systemuser"));
            ConEstado(constructor, Estados.De(estado));
            switch (estado)
            {
                case EstadoDeArreglo.Vigente:
                    constructor.Con("sanic_numeroarreglo", 1).Con("sanic_fechaformalizado", Ahora.AddDays(-1)).Con("sanic_formalizadoporid", Ref("systemuser"));
                    break;
                case EstadoDeArreglo.Terminado:
                    constructor.Con("sanic_numeroarreglo", 1).Con("sanic_fechatermino", Ahora.AddDays(-1));
                    break;
                case EstadoDeArreglo.Anulado:
                    constructor.Con("sanic_fechatermino", Ahora.AddDays(-1)).Con("sanic_fechaanulacion", Ahora.AddDays(-1))
                        .Con("sanic_motivoanulacion", "ZZF-PRUEBA motivo de anulación").Con("sanic_anuladoporid", Ref("systemuser"));
                    break;
                case EstadoDeArreglo.Descartado:
                    constructor.Con("sanic_fechatermino", Ahora.AddDays(-1)).Con("sanic_fechadescarte", Ahora.AddDays(-1))
                        .Con("sanic_motivodescarte", "ZZF-PRUEBA motivo de descarte").Con("sanic_descartadoporid", Ref("systemuser"));
                    break;
            }
            return constructor;
        }

        public static ConstructorDeFila UnaCuota()
        {
            var n = Siguiente();
            return ConEstadoActivo(ConPropietario(new ConstructorDeFila("sanic_carp_tbl_cuota"))
                .Con("sanic_nombre", "ARR-" + n.ToString("D6", CultureInfo.InvariantCulture) + "-C01")
                .Con("sanic_numero", 1)
                .Con("sanic_fechavencimiento", SoloFecha(2026, 11, 1))
                .Con("sanic_monto", Dec(150.00m))
                .Con("sanic_arregloid", Ref("sanic_carp_tbl_arreglo")));
        }

        /// <summary>Email entrante recibido: <c>directioncode</c> false, Completed / Received (<see cref="Estados.StatusCorreoRecibido"/>), sin <c>senton</c> (fixture 01).</summary>
        public static ConstructorDeFila UnCorreoEntrante()
        {
            return UnCorreo(false, Estados.StatusCorreoRecibido);
        }

        private static ConstructorDeFila UnCorreo(bool saliente, int statuscode)
        {
            var n = Siguiente();
            var constructor = new ConstructorDeFila("email")
                .Con("messageid", "<zzf-" + (saliente ? "saliente" : "entrante") + "-" + n.ToString(CultureInfo.InvariantCulture) + "@example.com>")
                .Con("subject", "ZZF-PRUEBA asunto " + n.ToString(CultureInfo.InvariantCulture))
                .Con("directioncode", saliente)
                .Con("statecode", new OptionSetValue(Estados.StateCorreoCompletado))
                .Con("statuscode", new OptionSetValue(statuscode))
                .Con("actualend", Ahora.AddHours(-3))
                .Con("createdon", Ahora.AddHours(-3))
                .Con("createdby", Ref("systemuser"))
                .Con("ownerid", Ref("systemuser"));
            // Un entrante no tiene senton (fixture 01): la columna está ausente, no nula.
            if (saliente) constructor.Con("senton", Ahora.AddHours(-2));
            return constructor;
        }

        /// <summary>Email saliente enviado: <c>directioncode</c> true, Completed / Sent (<see cref="Estados.StatusCorreoEnviado"/>), con <c>senton</c> (fixture 01).</summary>
        public static ConstructorDeFila UnCorreoSaliente()
        {
            return UnCorreo(true, Estados.StatusCorreoEnviado);
        }

        public static ConstructorDeFila UnaVersion(EstadoDeVersionDePlantilla estado = EstadoDeVersionDePlantilla.Borrador)
        {
            var n = Siguiente();
            var constructor = ConEstadoActivo(ConPropietario(new ConstructorDeFila("sanic_carp_tbl_versionplantilla"))
                .Con("sanic_nombre", "v" + (10 + n % 990).ToString(CultureInfo.InvariantCulture))
                .Con("sanic_capacidad", 60)
                .Con("sanic_estado", new OptionSetValue((int)estado)));
            if (estado != EstadoDeVersionDePlantilla.Borrador)
                constructor.Con("sanic_fechaaprobacion", Ahora.AddDays(-5)).Con("sanic_aprobadoporid", Ref("systemuser"));
            return constructor;
        }

        public static ConstructorDeFila UnMapeo()
        {
            const string hoja = " FORMATO ARP ELECTRONICO";   // con el espacio inicial, como la plantilla real (spike 1)
            const string celda = "B7";
            return ConEstadoActivo(ConPropietario(new ConstructorDeFila("sanic_carp_tbl_mapeocelda"))
                .Con("sanic_nombre", hoja + "!" + celda)
                .Con("sanic_hoja", hoja)
                .Con("sanic_celda", celda)
                .Con("sanic_tipo", new OptionSetValue((int)TipoDeMapeo.ValorSimple))
                .Con("sanic_campo", new OptionSetValue((int)CampoMapeable.FechaDeFormalizacion))
                .Con("sanic_versionplantillaid", Ref("sanic_carp_tbl_versionplantilla")));
        }

        // ---- Fuera de la lista de 4.5, pero necesarios para las tablas de los fixtures 4 a 6 (D-59 vive en teammembership) ----

        public static ConstructorDeFila UnCliente()
        {
            var n = Siguiente();
            var texto = n.ToString("D6", CultureInfo.InvariantCulture);
            return ConEstadoActivo(ConPropietario(new ConstructorDeFila("sanic_carp_tbl_cliente"))
                .Con("sanic_nombre", "ZZF cliente " + texto)
                .Con("sanic_nombrecompleto", "ZZF cliente " + texto)
                .Con("sanic_cedula", "ZZF-ced-" + texto));
        }

        /// <summary><c>teammembership</c>: exactamente <c>teammembershipid</c>, <c>systemuserid</c> y <c>teamid</c>, los tres <see cref="Guid"/> CRUDOS (fixture 04).</summary>
        public static ConstructorDeFila UnaMembresiaDeTeam(Guid usuario, Guid team)
        {
            // Una tabla intersect lleva Guid CRUDO en sus tres columnas, nunca EntityReference (defecto D-59 de M-PPP).
            return new ConstructorDeFila("teammembership").Con("systemuserid", usuario).Con("teamid", team);
        }

        public static ConstructorDeFila UnTeam(string nombre, Guid unidadDeNegocio, bool predeterminado)
        {
            return new ConstructorDeFila("team")
                .Con("name", nombre)
                .Con("businessunitid", new EntityReference("businessunit", unidadDeNegocio))
                .Con("isdefault", predeterminado);
        }

        public static ConstructorDeFila UnaCola(string correo)
        {
            return ConEstadoActivo(new ConstructorDeFila("queue")
                .Con("name", "ZZF Cola " + Siguiente().ToString(CultureInfo.InvariantCulture))
                .Con("emailaddress", correo));
        }

        /// <summary>Una persona: <c>fullname</c>, <c>isdisabled</c> false, <c>accessmode</c> 0, <c>businessunitid</c> (fixture 06).</summary>
        public static ConstructorDeFila UnUsuario(Guid id)
        {
            return new ConstructorDeFila("systemuser").ConId(id)
                .Con("fullname", "ZZF Usuario " + Siguiente().ToString(CultureInfo.InvariantCulture))
                .Con("isdisabled", false)
                .Con("accessmode", new OptionSetValue(ModoDeAccesoNormal))
                .Con("businessunitid", new EntityReference("businessunit", ConfiguracionSimulada.IdDeBuPorDefecto));
        }

        /// <summary>SYSTEM: <c>fullname</c> "SYSTEM", <c>isdisabled</c> TRUE, <c>accessmode</c> 0 (fixture 06; una regla "usuario habilitado" no aplica a SYSTEM).</summary>
        public static ConstructorDeFila UnSystem(Guid id)
        {
            return new ConstructorDeFila("systemuser").ConId(id)
                .Con("fullname", "SYSTEM")
                .Con("isdisabled", true)
                .Con("accessmode", new OptionSetValue(ModoDeAccesoNormal))
                .Con("businessunitid", new EntityReference("businessunit", ConfiguracionSimulada.IdDeBuPorDefecto));
        }

        /// <summary><c>environmentvariabledefinition</c> de tipo texto (<see cref="TipoDeVariableTexto"/>) con su valor por defecto.</summary>
        public static ConstructorDeFila UnaDefinicionDeVariable(string nombreDeEsquema, string valorPorDefecto)
        {
            return ConEstadoActivo(new ConstructorDeFila("environmentvariabledefinition")
                .Con("schemaname", nombreDeEsquema)
                .Con("displayname", "ZZF-" + nombreDeEsquema)
                .Con("defaultvalue", valorPorDefecto)
                .Con("type", new OptionSetValue(TipoDeVariableTexto)));
        }

        /// <summary><c>environmentvariablevalue</c> de una definición; <c>environmentvariabledefinitionid</c> es <c>EntityReference</c>.</summary>
        public static ConstructorDeFila UnValorDeVariable(string nombreDeEsquema, Guid definicion, string valor)
        {
            return ConEstadoActivo(new ConstructorDeFila("environmentvariablevalue")
                .Con("schemaname", nombreDeEsquema)
                .Con("value", valor)
                .Con("environmentvariabledefinitionid", new EntityReference("environmentvariabledefinition", definicion)));
        }
    }
}
