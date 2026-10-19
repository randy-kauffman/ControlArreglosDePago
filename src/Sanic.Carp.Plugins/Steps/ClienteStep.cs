using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-15 y S-16 (diseno/03 2.2 y 5), una sola clase con dos registros sobre <c>sanic_carp_tbl_cliente</c>: Create · PreOperation · síncrono · orden 10 · sin imagen, y
    /// Update · PreOperation · síncrono · orden 10 · filtro <c>sanic_nombre, sanic_nombrecompleto, sanic_cedula, sanic_correo, sanic_celular, statecode, statuscode</c> · pre-imagen
    /// <c>PreImagen</c> (<c>sanic_nombre</c>, <c>statecode</c>, <c>statuscode</c>). Decide Create o Update por <c>contexto.Mensaje</c>. El nombre de la clase y el namespace son definitivos.
    /// <para>
    /// Contrato (el constructor lo implementa; hoy <c>Ejecutar</c> lanza <see cref="NotImplementedException"/>). Los pasos son los de 2.2, en ese orden y ganando el primero
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

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            throw new NotImplementedException();
        }
    }
}
