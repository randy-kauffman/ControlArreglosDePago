using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using ArreglosPago.Formato;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace Spk.Plugins
{
    /// <summary>
    /// Plugin de la Custom API spk_GenerarFormatoSpike (Accion, global).
    /// Entrada: VersionId (Guid) y Plazo (Integer, 1 a 60). Salida: ArchivoBase64, Milisegundos, TamanoBytes y Detalle (diagnostico del spike).
    /// Descarga la plantilla de la columna File DENTRO del plugin, valida la huella, genera con datos ficticios fijos y devuelve el archivo.
    /// </summary>
    public class GenerarFormatoSpikePlugin : IPlugin
    {
        private const string Tabla = "spk_versionplantilla";
        private const string ColumnaArchivo = "spk_archivo";
        private const int BloqueDescarga = 4 * 1024 * 1024;

        // Estado del proceso: sirve para distinguir la primera llamada (en frio) de las siguientes en el mismo proceso del sandbox.
        private static int _llamadas;
        private static readonly DateTime _nacimientoUtc = DateTime.UtcNow;

        public void Execute(IServiceProvider serviceProvider)
        {
            var total = Stopwatch.StartNew();
            var llamada = Interlocked.Increment(ref _llamadas);
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var fabrica = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            trace.Trace("[GenerarFormatoSpike] START | llamada={0} depth={1} ensamblado={2}", llamada, context.Depth, typeof(GenerarFormatoSpikePlugin).Assembly.GetName().Version);

            try
            {
                // 1. Entradas: validacion de negocio antes de tocar nada.
                Guid versionId = LeerGuid(context, "VersionId");
                int plazo = LeerEntero(context, "Plazo");
                if (plazo < 1 || plazo > Campos.CapacidadMaxima)
                    throw new InvalidPluginExecutionException("El plazo debe estar entre 1 y " + Campos.CapacidadMaxima + " cuotas; se recibió " + plazo + ".");

                // 2. Descarga de la plantilla desde la columna File, con la identidad del llamador.
                var servicio = fabrica.CreateOrganizationService(context.UserId);
                var tDescarga = Stopwatch.StartNew();
                string huella;
                byte[] plantilla = DescargarPlantilla(servicio, versionId, trace, out huella);
                tDescarga.Stop();
                trace.Trace("[GenerarFormatoSpike] plantilla descargada: {0} bytes en {1} ms", plantilla.Length, tDescarga.ElapsedMilliseconds);

                // 3. Generacion (valida la huella y traduce todo error controlado a un mensaje de negocio).
                long memAntes = GC.GetTotalMemory(false);
                var tGen = Stopwatch.StartNew();
                byte[] archivo;
                try
                {
                    archivo = GeneradorFormato.Generar(plantilla, huella, DatosFicticios.Mapa(), DatosFicticios.Construir(plazo));
                }
                catch (FormatoException ex)
                {
                    trace.Trace("[GenerarFormatoSpike] error controlado {0}: {1} | interna: {2}", ex.GetType().Name, ex.Message, ex.InnerException == null ? "-" : ex.InnerException.GetType().Name + ": " + ex.InnerException.Message);
                    throw new InvalidPluginExecutionException("No se pudo generar el formato (" + ex.GetType().Name + "): " + ex.Message, ex);
                }
                tGen.Stop();

                var tB64 = Stopwatch.StartNew();
                string base64 = Convert.ToBase64String(archivo);
                tB64.Stop();
                total.Stop();

                context.OutputParameters["ArchivoBase64"] = base64;
                context.OutputParameters["Milisegundos"] = (int)total.ElapsedMilliseconds;
                context.OutputParameters["TamanoBytes"] = archivo.Length;
                context.OutputParameters["Detalle"] = Detalle(llamada, tDescarga.Elapsed, tGen.Elapsed, tB64.Elapsed, plantilla.Length, base64.Length, memAntes);
                trace.Trace("[GenerarFormatoSpike] END | ok {0} ms, {1} bytes", total.ElapsedMilliseconds, archivo.Length);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;   // ya viene con un mensaje para el usuario
            }
            catch (Exception ex)
            {
                // Spike: se informa el tipo y el mensaje reales para poder diagnosticar errores de carga de ensamblado.
                trace.Trace("[GenerarFormatoSpike] ERROR inesperado | {0}", ex.ToString());
                throw new InvalidPluginExecutionException("Error inesperado al generar el formato: " + ex.GetType().FullName + ": " + ex.Message, ex);
            }
        }

        private static Guid LeerGuid(IPluginExecutionContext c, string nombre)
        {
            object v;
            if (!c.InputParameters.TryGetValue(nombre, out v) || !(v is Guid) || (Guid)v == Guid.Empty)
                throw new InvalidPluginExecutionException("Falta el parámetro " + nombre + " o no es un identificador válido.");
            return (Guid)v;
        }

        private static int LeerEntero(IPluginExecutionContext c, string nombre)
        {
            object v;
            if (!c.InputParameters.TryGetValue(nombre, out v) || !(v is int))
                throw new InvalidPluginExecutionException("Falta el parámetro " + nombre + " o no es un número entero.");
            return (int)v;
        }

        private static byte[] DescargarPlantilla(IOrganizationService servicio, Guid id, ITracingService trace, out string huella)
        {
            Entity fila;
            try
            {
                fila = servicio.Retrieve(Tabla, id, new Microsoft.Xrm.Sdk.Query.ColumnSet("spk_huella", ColumnaArchivo, ColumnaArchivo + "_name"));
            }
            catch (System.ServiceModel.FaultException<OrganizationServiceFault> ex)
            {
                trace.Trace("[GenerarFormatoSpike] Retrieve fallo: {0} {1}", ex.Detail.ErrorCode, ex.Detail.Message);
                throw new InvalidPluginExecutionException("No se encontró la versión de plantilla " + id + " (o no hay permiso para leerla).");
            }
            huella = fila.GetAttributeValue<string>("spk_huella");
            if (!fila.Contains(ColumnaArchivo) || fila[ColumnaArchivo] == null)
                throw new InvalidPluginExecutionException("La versión de plantilla no tiene archivo cargado.");

            var inicio = (InitializeFileBlocksDownloadResponse)servicio.Execute(new InitializeFileBlocksDownloadRequest
            {
                Target = new EntityReference(Tabla, id),
                FileAttributeName = ColumnaArchivo
            });
            long tamano = inicio.FileSizeInBytes;
            long bloque = inicio.IsChunkingSupported ? Math.Min(BloqueDescarga, Math.Max(1, tamano)) : tamano;
            var salida = new byte[tamano];
            long offset = 0;
            while (offset < tamano)
            {
                var resp = (DownloadBlockResponse)servicio.Execute(new DownloadBlockRequest
                {
                    FileContinuationToken = inicio.FileContinuationToken,
                    Offset = offset,
                    BlockLength = bloque
                });
                Buffer.BlockCopy(resp.Data, 0, salida, (int)offset, resp.Data.Length);
                offset += resp.Data.Length;
                if (resp.Data.Length == 0) throw new InvalidPluginExecutionException("La descarga de la plantilla devolvió un bloque vacío.");
            }
            trace.Trace("[GenerarFormatoSpike] descarga: chunking={0} tamano={1} bloque={2}", inicio.IsChunkingSupported, tamano, bloque);
            return salida;
        }

        private static string Detalle(int llamada, TimeSpan descarga, TimeSpan generacion, TimeSpan base64, int bytesPlantilla, int largoBase64, long memAntes)
        {
            var sb = new StringBuilder();
            sb.Append("build=").Append(typeof(GenerarFormatoSpikePlugin).Assembly.GetName().Version);
            sb.Append(";llamadaEnElProceso=").Append(llamada);
            sb.Append(";procesoVivoMs=").Append((long)(DateTime.UtcNow - _nacimientoUtc).TotalMilliseconds);
            sb.Append(";descargaMs=").Append((long)descarga.TotalMilliseconds);
            sb.Append(";generacionMs=").Append((long)generacion.TotalMilliseconds);
            sb.Append(";base64Ms=").Append((long)base64.TotalMilliseconds);
            sb.Append(";bytesPlantilla=").Append(bytesPlantilla);
            sb.Append(";largoBase64=").Append(largoBase64);
            sb.Append(";memAntesKB=").Append(memAntes / 1024);
            sb.Append(";memDespuesKB=").Append(GC.GetTotalMemory(false) / 1024);
            sb.Append(";clr=").Append(Environment.Version);
            sb.Append(";procesadores=").Append(Sonda(() => Environment.ProcessorCount.ToString()));
            sb.Append(";so64=").Append(Sonda(() => Environment.Is64BitProcess.ToString()));
            sb.Append(";ensamblados=");
            try
            {
                var rel = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName())
                    .Where(n => n.Name.IndexOf("OpenXml", StringComparison.OrdinalIgnoreCase) >= 0 || n.Name.StartsWith("WindowsBase", StringComparison.Ordinal)
                                || n.Name.StartsWith("System.IO", StringComparison.Ordinal) || n.Name.StartsWith("ArreglosPago", StringComparison.Ordinal)
                                || n.Name.StartsWith("Spk", StringComparison.Ordinal))
                    .Select(n => n.Name + " " + n.Version).OrderBy(x => x);
                sb.Append(string.Join(" | ", rel));
            }
            catch (Exception ex) { sb.Append("no disponible: " + ex.GetType().Name); }
            return sb.ToString();
        }

        private static string Sonda(Func<string> f)
        {
            try { return f(); } catch (Exception ex) { return "no disponible (" + ex.GetType().Name + ")"; }
        }
    }
}
