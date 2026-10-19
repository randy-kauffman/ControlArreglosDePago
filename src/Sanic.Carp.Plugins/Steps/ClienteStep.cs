using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-15 y S-16 (diseno/03 2.2 y 5), una sola clase con dos registros sobre <c>sanic_carp_tbl_cliente</c>: Create · PreOperation · síncrono · orden 10 · sin imagen, y
    /// Update · PreOperation · síncrono · orden 10 · filtro <c>sanic_nombre, sanic_nombrecompleto, sanic_cedula, sanic_correo, sanic_celular, statecode, statuscode</c> · pre-imagen
    /// <c>PreImagen</c> (<c>sanic_nombre</c>, <c>statecode</c>, <c>statuscode</c>). Decide Create o Update por <c>contexto.Mensaje</c>. El nombre de la clase y el namespace son definitivos.
    /// <para>
    /// Contrato. Los pasos son los de 2.2, en ese orden y ganando el primero
    /// que rechaza. Notas de diseño que el texto de 2.2 deja al arquitecto (sublote 5a, 4.1/2.2):
    /// </para>
    /// <list type="bullet">
    /// <item>Una columna AUSENTE del <c>Target</c> de un <c>Create</c> es una columna vacía: sin nombre completo ⇒ <c>CARP-CLI-001</c>; sin cédula ⇒ <c>CARP-CLI-002</c> (la regla "obligatoria" vale aunque la clave no venga).</item>
    /// <item>La normalización usa <c>NormalizadorDeTexto</c> y se escribe en el <c>Target</c> solo de las columnas presentes (en <c>Update</c>, una columna ausente no se agrega al <c>Target</c>).</item>
    /// <item>La unicidad de la cédula (<c>Create</c>, y <c>Update</c> cuando la cédula cambió) usa <c>ClientesDataverse.BuscarPorCedula(cedulaNormalizada)</c> con el servicio de SYSTEM (DC-22); el <c>Update</c> excluye su propio id.</item>
    /// <item>En el <c>Update</c>, la cédula guardada se lee con el servicio de SYSTEM (por id, solo <c>sanic_cedula</c>; DC-28), nunca con el del llamador, y solo si el <c>Target</c> trae <c>sanic_cedula</c>.</item>
    /// <item>El <c>Update</c> solo consulta la identidad cuando el <c>Target</c> trae un <c>statecode</c>/<c>statuscode</c> o un <c>sanic_nombre</c> DISTINTOS de los de la pre-imagen (<c>Cambios</c>, DC-01): reenviar los mismos valores no es un cambio y no consulta nada (camino barato, PA-CLI-18).</item>
    /// <item>En el <c>Create</c>, un <c>sanic_nombre</c> que mande la persona se SOBRESCRIBE con el nombre completo normalizado (paso 3); no hay regla de estado ni de nombre. Un <c>Create</c> hecho por el sistema también normaliza y comprueba la unicidad (2.2 no lo exceptúa).</item>
    /// </list>
    /// Área de error: <c>CLI</c>.
    /// </summary>
    public sealed class ClienteStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Create", EtapaDelPipeline.PreOperation, false, 10, new[] { TablasDelArea.Cliente }, null, null),
            new RegistroEsperado("Update", EtapaDelPipeline.PreOperation, false, 10, new[] { TablasDelArea.Cliente },
                new[] { "sanic_nombre", "sanic_nombrecompleto", "sanic_cedula", "sanic_correo", "sanic_celular", "statecode", "statuscode" },
                new[] { "sanic_nombre", "statecode", "statuscode" })
        });

        internal override string Area { get { return "CLI"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        private const string ColNombre = "sanic_nombre";
        private const string ColNombreCompleto = "sanic_nombrecompleto";
        private const string ColCedula = "sanic_cedula";
        private const string ColCorreo = "sanic_correo";
        private const string ColCelular = "sanic_celular";
        private const string ColEstado = "statecode";
        private const string ColMotivoDeEstado = "statuscode";

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            if (string.Equals(contexto.Mensaje, "Create", StringComparison.Ordinal)) Crear(contexto);
            else Actualizar(contexto);
        }

        // 2.2, Create. Una columna ausente cuenta como vacia: la regla "obligatoria" no depende de que la clave venga (sublote 5a, punto 1).
        private static void Crear(ContextoDeStep contexto)
        {
            var target = contexto.Target;
            var nombre = NormalizadorDeTexto.NombreCompleto(TextoDe(target, ColNombreCompleto));
            var cedula = NormalizadorDeTexto.Cedula(TextoDe(target, ColCedula));

            // Paso 1: los cuatro datos normalizados al Target. Las columnas ausentes siguen ausentes (PA-CLI-06).
            if (target.Contains(ColNombreCompleto)) target[ColNombreCompleto] = nombre;
            if (target.Contains(ColCedula)) target[ColCedula] = cedula;
            if (target.Contains(ColCorreo)) target[ColCorreo] = NormalizadorDeTexto.Correo(TextoDe(target, ColCorreo));
            if (target.Contains(ColCelular)) target[ColCelular] = NormalizadorDeTexto.Celular(TextoDe(target, ColCelular));

            // Paso 2, en el orden escrito: con los dos vacios gana el nombre (PA5A_CLI_04).
            if (nombre == null) throw CodigosDeError.CLI_001.Crear();
            if (cedula == null) throw CodigosDeError.CLI_002.Crear();

            // Paso 3: la primaria calculada; lo que mande la persona se sobrescribe (sublote 5a, punto 2).
            target[ColNombre] = nombre;

            // Paso 4: unicidad con SYSTEM, activos e inactivos (DC-22). Create y Update del sistema tambien: 2.2 no lo exceptua.
            contexto.Trazar("unicidad de la cedula (alta)");
            if (Clientes(contexto).BuscarPorCedula(cedula).Count > 0) throw CodigosDeError.CLI_003.Crear();
        }

        // 2.2, Update: gana la primera regla que rechaza (0, 0 bis, 1, 2; sublote 5a, punto 5).
        private static void Actualizar(ContextoDeStep contexto)
        {
            var target = contexto.Target;
            var neutro = contexto.TargetNeutro();
            var previa = contexto.PreImagenNeutra();

            // Pasos 0 y 0 bis. Camino barato (0.3): la identidad solo se consulta si hubo un cambio de estado o de nombre mostrado respecto de la
            // pre-imagen (DC-01); reenviar el mismo valor no es un cambio (PA-CLI-18). Para el sistema no hay ninguna de las dos reglas (DC-03).
            var cambioDeEstado = Cambios.Distinto(neutro, previa, ColEstado) || Cambios.Distinto(neutro, previa, ColMotivoDeEstado);
            var cambioDeNombreMostrado = Cambios.Distinto(neutro, previa, ColNombre);
            if ((cambioDeEstado || cambioDeNombreMostrado) && !contexto.Identidad.EsEscrituraDelSistema)
                throw cambioDeEstado ? CodigosDeError.CLI_004.Crear() : CodigosDeError.CLI_005.Crear();

            // Paso 1: solo las columnas presentes.
            if (target.Contains(ColNombreCompleto))
            {
                var nombre = NormalizadorDeTexto.NombreCompleto(TextoDe(target, ColNombreCompleto));
                if (nombre == null) throw CodigosDeError.CLI_001.Crear();
                target[ColNombreCompleto] = nombre;
                // La pre-imagen trae la copia (sanic_nombre) y no el nombre completo: si la copia ya es este nombre no hay nada que recalcular.
                if (previa == null || Cambios.ValoresDistintos(nombre, ValorDe(previa, ColNombre))) target[ColNombre] = nombre;
            }
            if (target.Contains(ColCorreo)) target[ColCorreo] = NormalizadorDeTexto.Correo(TextoDe(target, ColCorreo));
            if (target.Contains(ColCelular)) target[ColCelular] = NormalizadorDeTexto.Celular(TextoDe(target, ColCelular));

            // Paso 2.
            if (!target.Contains(ColCedula)) return;
            var cedula = NormalizadorDeTexto.Cedula(TextoDe(target, ColCedula));
            if (cedula == null) throw CodigosDeError.CLI_002.Crear();
            target[ColCedula] = cedula;

            // La cedula guardada se lee siempre con SYSTEM (DC-28): el llamador sin perfil de columna la veria vacia y vera un cambio que no existe.
            var clientes = Clientes(contexto);
            var guardada = clientes.Leer(contexto.IdPrimario, new[] { ColCedula });
            if (guardada != null && string.Equals(ValorDe(guardada.Valores, ColCedula) as string, cedula, StringComparison.Ordinal)) return;

            contexto.Trazar("unicidad de la cedula (cambio)");
            foreach (var otro in clientes.BuscarPorCedula(cedula))
                if (otro != contexto.IdPrimario) throw CodigosDeError.CLI_003.Crear();
        }

        // Los dos servicios son el de SYSTEM: BuscarPorCedula lo usa siempre y Leer pasa a ser tambien "como sistema" (DC-22, DC-28).
        private static IClientes Clientes(ContextoDeStep contexto)
        {
            return new ClientesDataverse(contexto.ServicioComoSistema, contexto.ServicioComoSistema);
        }

        private static string TextoDe(Entity entidad, string columna)
        {
            return entidad.Contains(columna) ? entidad[columna] as string : null;
        }

        private static object ValorDe(IReadOnlyDictionary<string, object> valores, string columna)
        {
            object valor;
            return valores.TryGetValue(columna, out valor) ? valor : null;
        }
    }
}
