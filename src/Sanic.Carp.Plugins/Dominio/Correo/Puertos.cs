using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio.Correo
{
    /// <summary>
    /// La lectura con SYSTEM que <see cref="ReglasDeCorreo.EvaluarColumnasDelSistema"/> pide solo cuando hace falta (diseno/03 2.9 y "Decisiones del arquitecto, 2026-10-19 (sublote 5f)").
    /// Lo implementa <c>Datos/LecturasDeCorreoDataverse</c>. Existe porque S-33 NO tiene pre-imagen (sección 5): para saber si <c>sanic_tiemporespuesta</c> o
    /// <c>sanic_respondidoporid</c> "cambian" (PA-ESI-04) hay que mirar el valor guardado, y ese valor se lee con SYSTEM solo si quien escribe es una persona y el <c>Update</c> trae alguna
    /// de las dos columnas. Es SYSTEM y no el llamador porque la lectura no debe depender de que la persona vea la fila. Una falla de lectura distinta de "no existe" o "sin acceso" escapa.
    /// </summary>
    public interface ILecturasDeCorreo
    {
        /// <summary>
        /// Los valores guardados de <c>sanic_tiemporespuesta</c> y <c>sanic_respondidoporid</c> del correo (UNA consulta por id, solo esas dos columnas), en neutro (<c>int</c> y
        /// <see cref="ReferenciaDeEntidad"/>). El diccionario trae SIEMPRE las dos claves, con <c>null</c> en la columna que la fila no tiene (la plataforma no devuelve las nulas;
        /// el adaptador las completa, igual que un step completa su pre-imagen) y en las dos si la fila no existe o no se puede leer.
        /// </summary>
        IReadOnlyDictionary<string, object> ColumnasDelSistema(Guid emailId);
    }

    /// <summary>
    /// La lectura con SYSTEM que <see cref="ReglasDeCorreo.EvaluarRemitenteEnActualizacion"/> pide solo cuando el <c>Target</c> de un <c>Update</c> no le alcanza (S-37, diseno/03 2.9 y "Decisiones del
    /// arquitecto, 2026-10-19 (cierre del sublote 5f)", punto 1). Lo implementa <c>Datos/LecturasDeCorreoDataverse</c>, la misma clase que <see cref="ILecturasDeCorreo"/>. Existe porque S-37 NO tiene
    /// pre-imagen (sección 5): para saber si el correo, DESPUÉS del <c>Update</c>, es saliente y está en una conversación hay que mirar lo guardado de las columnas que el <c>Target</c> no trae. El <c>from</c>
    /// NO se lee nunca (un <c>Retrieve</c> con columnas no trae las partes y el diseño prohíbe <c>ColumnSet(true)</c> sobre <c>email</c>): el step lo reemplaza por la queue cuando el <c>Target</c> no lo trae.
    /// </summary>
    public interface ILecturasDelRemitente
    {
        /// <summary>
        /// Los valores guardados de <c>directioncode</c> y <c>regardingobjectid</c> del correo (UNA consulta por id, solo esas dos columnas, en este orden), en neutro (<c>bool</c> y
        /// <see cref="ReferenciaDeEntidad"/>). El diccionario trae SIEMPRE las dos claves, con <c>null</c> en la columna que la fila no tiene (la plataforma no devuelve las nulas; el adaptador las completa)
        /// y en las dos si la fila no existe o no se puede leer. Una falla distinta de "no existe" o "sin acceso" escapa.
        /// </summary>
        IReadOnlyDictionary<string, object> ColumnasDelRemitente(Guid emailId);
    }
}
