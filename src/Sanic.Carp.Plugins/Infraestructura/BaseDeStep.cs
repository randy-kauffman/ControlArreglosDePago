using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Errores;

namespace Sanic.Carp.Plugins.Infraestructura
{
    /// <summary>
    /// Base de TODOS los steps del paquete (diseno/03 4.1, "Decisiones del arquitecto, 2026-10-19 (sublote 5a)"). Es la única que implementa
    /// <see cref="IPlugin"/>: <see cref="Execute"/> no se puede sobrescribir, de modo que ningún step se salta la verificación del registro, la
    /// traza segura ni la conversión de errores. El step solo declara <see cref="Area"/> y <see cref="RegistrosEsperados"/> y escribe
    /// <see cref="Ejecutar"/>; no tiene campos de instancia (Dataverse reutiliza la instancia entre hilos), ni constructor con configuración (no hay
    /// <c>unsecure</c> ni <c>secure</c>: la configuración sale de las variables de entorno por <see cref="ContextoDeStep.Configuracion"/>).
    /// <para>
    /// <b>Qué hace <see cref="Execute"/> (contrato; el constructor lo implementa, hoy lanza <see cref="NotImplementedException"/>):</b>
    /// <list type="number">
    /// <item><c>serviceProvider</c> nulo ⇒ <see cref="ArgumentNullException"/> (parámetro <c>serviceProvider</c>). Obtiene <c>IPluginExecutionContext</c>,
    /// <c>IOrganizationServiceFactory</c> e <c>ITracingService</c> del proveedor.</item>
    /// <item><b>Verifica el registro</b> contra <see cref="RegistrosEsperados"/> ANTES de nada más: el contexto tiene que coincidir con ALGUNO de los
    /// registros en mensaje (ordinal), etapa, modo (síncrono o asíncrono) y tabla (<c>PrimaryEntityName</c>, ordinal), y si ese registro lleva
    /// pre-imagen, el contexto tiene que traer <c>PreImagen</c>. Si no, lanza <see cref="InvalidPluginExecutionException"/> con un mensaje en español que nombra la
    /// clase del step, lo que llegó (mensaje y tabla; y <c>PreImagen</c> si es lo que falta) y lo que se esperaba, SIN llamar a <see cref="Ejecutar"/> y SIN convertirlo en
    /// <c>-900</c>: es un error de registro, no de negocio, y no tiene código <c>CARP</c> (el catálogo de <c>Dominio</c> está cerrado).</item>
    /// <item>Arma el <see cref="ContextoDeStep"/> de esta ejecución, traza la entrada (<c>[Clase] …</c> con mensaje, tabla, id primario, etapa, modo y profundidad: solo
    /// ids) y llama a <see cref="Ejecutar"/>; al terminar bien traza la salida.</item>
    /// <item><see cref="Dominio.Errores.ErrorDeNegocio"/> ⇒ <see cref="InvalidPluginExecutionException"/> cuyo mensaje es EXACTAMENTE <c>error.ToString()</c>
    /// (<c>"CARP-XXX-NNN: texto"</c>); traza el código. Incluye los <c>CARP-CFG-*</c> que lanzan los puertos de configuración.</item>
    /// <item><see cref="InvalidPluginExecutionException"/> (la lanzó el step o un step anidado) ⇒ se relanza LA MISMA instancia.</item>
    /// <item>Cualquier otra excepción (una <c>FaultException</c> de la plataforma, un error de programación) ⇒ <see cref="InvalidPluginExecutionException"/> con
    /// <c>"CARP-" + Area + "-900: " + mensaje</c>, el mensaje de la excepción recortado a 300 unidades UTF-16 (0.5). La traza lleva solo el TIPO de la excepción
    /// (y la pila), nunca su mensaje, que puede traer valores del cliente.</item>
    /// <item>Ningún <c>catch</c> convierte una falla de ESCRITURA en continuar: una falla atrapada dentro de un plugin aborta la transacción (<c>0x8009000C</c>, medido,
    /// 0.5); por eso la conversión siempre relanza.</item>
    /// </list>
    /// La traza usa siempre <see cref="ContextoDeStep.Trazar"/> (<c>Trace("{0}", texto)</c>): nunca un valor del cliente (0.5).
    /// </para>
    /// </summary>
    public abstract class BaseDeStep : IPlugin
    {
        /// <summary>Sigla de la tabla en los códigos de error (<c>PRO</c>, <c>CLI</c>, <c>CNV</c>…): da el <c>CARP-&lt;Area&gt;-900</c> de las excepciones no previstas (0.5).</summary>
        internal abstract string Area { get; }

        /// <summary>Los registros que este step espera en la plataforma (sección 5). Siempre la misma lista de solo lectura: no es estado de la ejecución.</summary>
        internal abstract IReadOnlyList<RegistroEsperado> RegistrosEsperados { get; }

        /// <summary>
        /// La regla del step. Recibe el contexto de ESTA ejecución. Rechaza lanzando <see cref="Dominio.Errores.ErrorDeNegocio"/> (<c>throw resultado.Error;</c>) y no
        /// atrapa fallas de escritura. No guarda nada en campos.
        /// </summary>
        internal abstract void Ejecutar(ContextoDeStep contexto);

        /// <summary>Punto de entrada de la plataforma. No se sobrescribe. Contrato en la descripción de la clase.</summary>
        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

            var contextoDeLaPlataforma = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var fabrica = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var traza = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var contexto = new ContextoDeStep(GetType().Name, contextoDeLaPlataforma, fabrica, traza);

            // Primero el registro: un step mal registrado no debe ni tocar la identidad ni la configuracion (4.1, punto 5).
            VerificarElRegistro(contexto);

            contexto.Trazar("entrada mensaje=" + contexto.Mensaje + " tabla=" + contexto.Tabla + " id=" + contexto.IdPrimario.ToString("D")
                + " etapa=" + contexto.Etapa + " modo=" + (contexto.EsAsincrono ? "asincrono" : "sincrono")
                + " profundidad=" + contexto.Profundidad.ToString(CultureInfo.InvariantCulture));
            try
            {
                Ejecutar(contexto);
                contexto.Trazar("salida correcta");
            }
            // La conversion SIEMPRE relanza: una falla de escritura atrapada para "seguir" aborta la transaccion (0x8009000C, 0.5).
            catch (ErrorDeNegocio error)
            {
                contexto.Trazar("rechazo " + error.Codigo);
                throw new InvalidPluginExecutionException(error.ToString());
            }
            catch (InvalidPluginExecutionException)
            {
                // La lanzo el step o un step anidado: ya trae su texto. Su mensaje podria repetir un valor del cliente, asi que no se traza.
                contexto.Trazar("rechazo propagado de un step anidado");
                throw;
            }
            catch (Exception error)
            {
                // Solo el TIPO y la pila: el mensaje de la plataforma puede repetir valores del cliente (0.5).
                contexto.Trazar("falla inesperada " + error.GetType().Name);
                contexto.Trazar(error.StackTrace);
                throw new InvalidPluginExecutionException("CARP-" + Area + "-900: " + Recortar(error.Message));
            }
        }

        private const int LargoMaximoDelMensaje = 300;

        // 0.5: el mensaje de la plataforma se recorta a 300 unidades UTF-16. Si el corte partiera un par de sustitutos se corta un caracter antes: un
        // sustituto suelto no se puede serializar en la respuesta de error.
        private static string Recortar(string mensaje)
        {
            if (mensaje == null) return string.Empty;
            if (mensaje.Length <= LargoMaximoDelMensaje) return mensaje;
            var largo = LargoMaximoDelMensaje;
            if (char.IsHighSurrogate(mensaje[largo - 1]) && char.IsLowSurrogate(mensaje[largo])) largo--;
            return mensaje.Substring(0, largo);
        }

        private void VerificarElRegistro(ContextoDeStep contexto)
        {
            var esperados = RegistrosEsperados;
            var coincidentes = new List<RegistroEsperado>();
            foreach (var registro in esperados)
            {
                if (!string.Equals(registro.Mensaje, contexto.Mensaje, StringComparison.Ordinal)) continue;
                if (registro.Etapa != contexto.Etapa || registro.Asincrono != contexto.EsAsincrono) continue;
                if (!ContieneOrdinal(registro.Tablas, contexto.Tabla)) continue;
                coincidentes.Add(registro);
            }

            var nombre = contexto.NombreDelStep;
            var llego = "llegó " + contexto.Mensaje + " sobre " + contexto.Tabla + " en la etapa " + contexto.Etapa + " en modo "
                + (contexto.EsAsincrono ? "asíncrono" : "síncrono");

            if (coincidentes.Count == 0)
            {
                throw new InvalidPluginExecutionException("El step " + nombre + " está mal registrado en la plataforma: " + llego
                    + ". Se esperaba " + Describir(esperados) + ".");
            }

            if (contexto.PreImagen != null) return;
            foreach (var registro in coincidentes)
                if (!registro.LlevaPreImagen) return;

            throw new InvalidPluginExecutionException("El step " + nombre + " está mal registrado en la plataforma: " + llego
                + ", pero falta la pre-imagen " + ContextoDeStep.NombreDeLaPreImagen + " que su registro exige.");
        }

        private static bool ContieneOrdinal(IReadOnlyList<string> nombres, string buscado)
        {
            foreach (var nombre in nombres)
                if (string.Equals(nombre, buscado, StringComparison.Ordinal)) return true;
            return false;
        }

        private static string Describir(IReadOnlyList<RegistroEsperado> registros)
        {
            var partes = new List<string>();
            foreach (var registro in registros)
            {
                partes.Add(registro.Mensaje + " en " + string.Join(", ", registro.Tablas) + " (" + registro.Etapa + ", "
                    + (registro.Asincrono ? "asíncrono" : "síncrono") + (registro.LlevaPreImagen ? ", con pre-imagen " + ContextoDeStep.NombreDeLaPreImagen : string.Empty) + ")");
            }
            return string.Join("; ", partes);
        }
    }
}
