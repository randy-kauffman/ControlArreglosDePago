using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Dominio.Arreglo
{
    /// <summary>
    /// Lo que <see cref="ReglasDeDocumentoFirmado.Evaluar"/> necesita de UN guardado del arreglo que toca el documento firmado (diseno/03 2.6, "Documento firmado", S-38). Sin pre-imagen: la
    /// ficha sale de UNA lectura con SYSTEM, y solo si hace falta.
    /// </summary>
    public sealed class PedidoDeDocumento
    {
        /// <summary>El id del arreglo (<c>PrimaryEntityId</c> del <c>Update</c>).</summary>
        public Guid ArregloId { get; }

        /// <summary>Lo que el llamador manda (o lo que la plataforma agregó: el id del archivo cuando es una subida).</summary>
        public IReadOnlyDictionary<string, object> Target { get; }

        /// <summary>Quién escribe: <c>InitiatingUserId</c>, nunca <c>UserId</c> (0.3, V-4).</summary>
        public IIdentidadDeEjecucion Identidad { get; }

        /// <summary>La lectura con SYSTEM del estado, el congelamiento y el archivo.</summary>
        public ILecturasDelDocumento Lecturas { get; }

        /// <summary>Cualquier referencia nula ⇒ <see cref="ArgumentNullException"/>; un id vacío ⇒ <see cref="ArgumentException"/>.</summary>
        public PedidoDeDocumento(Guid arregloId, IReadOnlyDictionary<string, object> target, IIdentidadDeEjecucion identidad, ILecturasDelDocumento lecturas)
        {
            if (arregloId == Guid.Empty) throw new ArgumentException("Falta el id del arreglo.", nameof(arregloId));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            if (lecturas == null) throw new ArgumentNullException(nameof(lecturas));
            ArregloId = arregloId;
            Target = target;
            Identidad = identidad;
            Lecturas = lecturas;
        }
    }

    /// <summary>
    /// El documento firmado del arreglo (diseno/03 2.6, "Documento firmado"; pedido del usuario en la prueba guiada, 2026-11-03). Dominio puro: sin SDK, sin estado, identidad y lectura perezosas.
    /// <para>
    /// <b>Tres disparadores</b>, todos detectados por lo que el <c>Target</c> TRAE (el filtro del step es <see cref="ColumnasDelFiltro"/>):
    /// (1) <b>subida o reemplazo</b>: el <c>Target</c> trae <c>sanic_documentofirmado</c> (la plataforma lo provoca al subir el archivo, igual que en <c>sanic_archivo</c> de la versión, V-4); (2) <b>Formalizar</b>:
    /// el <c>Target</c> pide el par Vigente; (3) <b>Desbloquear</b>: el <c>Target</c> trae <c>sanic_fechacongelamiento</c> en <c>null</c>. Ninguno ⇒ permite sin leer ni consultar nada.
    /// </para>
    /// <para>
    /// <b>Pasos</b> (gana el primero que rechaza). El sistema (<c>EsEscrituraDelSistema</c>, UNA consulta) ⇒ permite (DC-03). Después UNA lectura de la ficha.
    /// <b>Subida</b>: la ficha no existe, o el arreglo no es un Borrador congelado ni un Vigente reabierto y vuelto a congelar (<see cref="FichaDelDocumentoFirmado.ReabiertoYRecongelado"/>) ⇒ <c>CARP-ARR-027</c>; con nombre de archivo conocido y de un tipo no permitido ⇒ <c>CARP-ARR-026</c> (si el nombre todavía
    /// no está, la barrera es Formalizar). <b>Formalizar</b> (solo si el origen es Borrador; los demás orígenes los rechaza S-21): sin archivo ⇒ <c>CARP-ARR-025</c>; con archivo cuyo nombre falta o no es de un
    /// tipo permitido ⇒ <c>CARP-ARR-026</c> (falla cerrado). <b>Desbloquear</b> (origen Borrador congelado con archivo) ⇒ <c>CARP-ARR-028</c>: el documento corresponde al formato que se va a invalidar y una persona
    /// no puede borrar un archivo escribiendo <c>null</c> (solo <c>DeleteFile</c> o <c>DELETE</c> a la columna), así que se pide quitarlo antes.
    /// </para>
    /// Nunca escribe. Los tipos permitidos son la extensión del nombre: .pdf, .jpg, .jpeg y .png, sin distinguir mayúsculas (<see cref="TipoPermitido"/>).
    /// </summary>
    public static class ReglasDeDocumentoFirmado
    {
        private const string ColDocumento = "sanic_documentofirmado";
        private const string ColCongelamiento = "sanic_fechacongelamiento";
        private const string ColEstado = "statecode";
        private const string ColMotivoDeEstado = "statuscode";

        private static readonly IReadOnlyList<string> Filtro = new ReadOnlyCollection<string>(new[] { ColDocumento, ColEstado, ColMotivoDeEstado, ColCongelamiento });

        /// <summary>El filtro de atributos del registro de S-38: el documento, el estado (Formalizar) y el congelamiento (Desbloquear). El orden es el de la sección 5 de diseno/03.</summary>
        public static IReadOnlyList<string> ColumnasDelFiltro { get { return Filtro; } }

        /// <summary>
        /// ¿El nombre del archivo termina en .pdf, .jpg, .jpeg o .png (sin distinguir mayúsculas)? Un nombre nulo, vacío o que es solo la extensión ⇒ <c>false</c>. Comparación ordinal.
        /// </summary>
        public static bool TipoPermitido(string nombreDelArchivo)
        {
            if (string.IsNullOrWhiteSpace(nombreDelArchivo)) return false;
            var nombre = nombreDelArchivo.Trim();
            return TerminaEn(nombre, ".pdf") || TerminaEn(nombre, ".jpg") || TerminaEn(nombre, ".jpeg") || TerminaEn(nombre, ".png");
        }

        private static bool TerminaEn(string nombre, string extension)
        {
            return nombre.Length > extension.Length && nombre.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Evalúa UN guardado. Contrato en la descripción de la clase. <c>pedido</c> nulo ⇒ <see cref="ArgumentNullException"/>.</summary>
        public static ResultadoDeEvaluacion Evaluar(PedidoDeDocumento pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));
            var target = pedido.Target;

            var sube = target.ContainsKey(ColDocumento);
            object congelamiento;
            var desbloquea = target.TryGetValue(ColCongelamiento, out congelamiento) && congelamiento == null;
            var pideVigente = PideElEstadoVigente(target);
            if (!sube && !desbloquea && !pideVigente) return ResultadoDeEvaluacion.Permitir();

            // DC-03: el sistema no se revalida.
            if (pedido.Identidad.EsEscrituraDelSistema) return ResultadoDeEvaluacion.Permitir();

            var ficha = pedido.Lecturas.FichaDelArreglo(pedido.ArregloId);

            if (sube)
            {
                if (!PuedeCambiarElDocumento(ficha)) return Rechazo(CodigosDeError.ARR_027);
                if (!string.IsNullOrWhiteSpace(ficha.NombreDelArchivo) && !TipoPermitido(ficha.NombreDelArchivo)) return Rechazo(CodigosDeError.ARR_026);
            }

            // Formalizar y Desbloquear solo existen desde un Borrador; los demas origenes los resuelve S-21 con su propio codigo.
            if (ficha == null || ficha.Estado != EstadoDeArreglo.Borrador) return ResultadoDeEvaluacion.Permitir();

            if (pideVigente)
            {
                if (!ficha.TieneArchivo) return Rechazo(CodigosDeError.ARR_025);
                if (!TipoPermitido(ficha.NombreDelArchivo)) return Rechazo(CodigosDeError.ARR_026);
            }

            if (desbloquea && ficha.Congelado && ficha.TieneArchivo) return Rechazo(CodigosDeError.ARR_028);

            return ResultadoDeEvaluacion.Permitir();
        }

        // Se adjunta o reemplaza en un Borrador congelado, o en un Vigente reabierto y vuelto a congelar (2026-11-03: se firma un papel nuevo). Un Vigente recien formalizado, o
        // reabierto y todavia sin congelar, no; tampoco un Terminado, Anulado o Descartado. Sin ficha, falla cerrado.
        private static bool PuedeCambiarElDocumento(FichaDelDocumentoFirmado ficha)
        {
            if (ficha == null) return false;
            if (ficha.Estado == EstadoDeArreglo.Borrador) return ficha.Congelado;
            return ficha.Estado == EstadoDeArreglo.Vigente && ficha.ReabiertoYRecongelado;
        }

        // El par que dejaria el Target es el Vigente (statuscode Vigente y, si viene, statecode Activo). Solo mira el Target: el origen se confirma despues con la ficha.
        private static bool PideElEstadoVigente(IReadOnlyDictionary<string, object> target)
        {
            var vigente = Estados.De(EstadoDeArreglo.Vigente);
            object motivo;
            if (!target.TryGetValue(ColMotivoDeEstado, out motivo)) return false;
            var codigoDelMotivo = Entero(motivo);
            if (!codigoDelMotivo.HasValue || codigoDelMotivo.Value != vigente.StatusCode) return false;
            object estado;
            if (!target.TryGetValue(ColEstado, out estado)) return true;
            var codigoDelEstado = Entero(estado);
            return !codigoDelEstado.HasValue || codigoDelEstado.Value == vigente.StateCode;
        }

        private static int? Entero(object valor)
        {
            var opcion = valor as ValorDeOpcion;
            if (opcion != null) return opcion.Valor;
            if (valor is int) return (int)valor;
            return null;
        }

        private static ResultadoDeEvaluacion Rechazo(DefinicionDeError definicion)
        {
            return ResultadoDeEvaluacion.Rechazar(definicion.Crear());
        }
    }
}
