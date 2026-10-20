using System;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Errores;

namespace Sanic.Carp.Plugins.Infraestructura
{
    /// <summary>
    /// Base de las cinco Custom API del paquete (diseno/03 4.1, "Decisiones del arquitecto, 2026-10-19 (lote 6)"). Es la ÚNICA clase de <c>Api/</c> que implementa
    /// <see cref="IPlugin"/>: <see cref="Execute"/> no se puede sobrescribir, de modo que ninguna API se salta la verificación del mensaje, la traza segura ni la
    /// traducción de errores. Cada API es una clase <c>public sealed</c> sin campos de instancia (Dataverse reutiliza la instancia entre hilos) que declara los
    /// miembros <c>internal</c> de abajo y delega el trabajo en su clase de lógica.
    /// <para>
    /// <b>Por qué no hereda de <see cref="BaseDeStep"/>.</b> (a) <c>BaseDeStep.Execute</c> compara el contexto con <see cref="RegistroEsperado"/>, que solo admite
    /// <c>Create</c>, <c>Update</c> y <c>Delete</c> en PreOperation o PostOperation (DC-27); el contexto de una Custom API trae como <c>MessageName</c> el nombre único de la
    /// API, no tiene <c>Target</c> ni pre-imagen y su <c>PrimaryEntityName</c> no es una tabla del área. (b) El <c>-900</c> de una API es un texto FIJO del catálogo
    /// (<c>CARP-GEN-900</c>: "No se pudo generar el formato… Intente de nuevo."), no el mensaje de la plataforma recortado a 300 que arma <c>BaseDeStep</c> (0.5, 4.1 punto 7).
    /// (c) La guardia <c>PA5A_REG_04</c> exige que todo tipo de <c>Steps/</c> sea un step, y 0.1 pone las API en <c>Sanic.Carp.Plugins.Api</c>. Lo que SÍ se comparte es el
    /// contexto: <see cref="ContextoDeApi"/> envuelve a <see cref="ContextoDeStep"/> (servicios, configuración, reloj y traza perezosos y memoizados por ejecución).
    /// Lo único que se repite es el reconocimiento del rechazo anidado <c>0x80040265</c> (unas líneas): <c>BaseDeStep</c> pertenece a un lote cerrado.
    /// </para>
    /// <para>
    /// <b>Qué hace <see cref="Execute"/> (contrato):</b>
    /// <list type="number">
    /// <item><c>serviceProvider</c> nulo ⇒ <see cref="ArgumentNullException"/> (parámetro <c>serviceProvider</c>). Obtiene <c>IPluginExecutionContext</c>, <c>IOrganizationServiceFactory</c>
    /// e <c>ITracingService</c> del proveedor y arma el <see cref="ContextoDeApi"/> de ESTA ejecución. El actor (<c>InitiatingUserId</c>) se captura en ese momento, antes de que
    /// nadie cree un servicio elevado (0.2, A-23).</item>
    /// <item><b>Verifica el mensaje</b> ANTES de nada más: <c>MessageName</c> debe ser igual (ordinal) a <see cref="NombreDeLaApi"/>. Si no, lanza
    /// <see cref="InvalidPluginExecutionException"/> con un texto en español que nombra la clase, lo que llegó y lo que se esperaba, SIN llamar a <see cref="Ejecutar"/> y SIN
    /// convertirlo en <c>-900</c> (es un error de registro, no de negocio, y no tiene código <c>CARP</c>). No verifica la etapa ni <c>PrimaryEntityName</c>: no están medidos
    /// dentro del sandbox (H-29, 8.1).</item>
    /// <item>Traza la entrada (<c>[Clase] entrada profundidad=…</c>: solo el nombre y la profundidad; ni parámetros ni valores) y llama a <see cref="Ejecutar"/>; al terminar bien
    /// traza la salida.</item>
    /// <item><see cref="ErrorDeNegocio"/> ⇒ <see cref="InvalidPluginExecutionException"/> cuyo mensaje es EXACTAMENTE <c>error.ToString()</c> (<c>"CARP-XXX-NNN: texto"</c>), también los
    /// <c>CARP-CFG-*</c> que lanzan los puertos de configuración. Traza solo el código.</item>
    /// <item><see cref="InvalidPluginExecutionException"/> (la lanzó la API o un step anidado) ⇒ se relanza LA MISMA instancia.</item>
    /// <item><see cref="System.ServiceModel.FaultException{TDetail}"/> de <see cref="OrganizationServiceFault"/> con <c>ErrorCode</c> <c>0x80040265</c> y mensaje no vacío (un step anidado rechazó la
    /// escritura, 0.5, <c>RESULTADO.md</c> sección 4) ⇒ <see cref="InvalidPluginExecutionException"/> con ese mensaje TAL CUAL: ya es <c>CARP-XXX-NNN: …</c>; ni <c>-900</c> ni recorte ni
    /// el código de la API (un rechazo de negocio no se presenta como reintentable).</item>
    /// <item>Cualquier otra excepción (una <c>FaultException</c> de la plataforma: clave K-01 <c>0x80060892</c>, versión de fila <c>0x80060882</c>, privilegio, transacción abortada
    /// <c>0x8009000C</c>; un error de programación) ⇒ <see cref="InvalidPluginExecutionException"/> con <c>ErrorTransitorio.Crear().ToString()</c>: el texto fijo del catálogo de esa
    /// API (<c>CARP-GEN-900</c>, <c>-REC-900</c>, <c>-TOM-900</c>, <c>-UNI-900</c>, <c>-INI-900</c>), que es reintentable (0.5). La traza lleva solo el TIPO de la excepción y, si es una
    /// <c>FaultException</c>, su <c>ErrorCode</c> en hexadecimal (<c>"0x" + código.ToString("X8")</c>); nunca el mensaje, que puede repetir valores del cliente.</item>
    /// <item>NINGÚN <c>catch</c> convierte una falla de ESCRITURA en continuar: una falla de datos atrapada dentro de un plugin aborta la transacción (<c>0x8009000C</c>, medido, 0.5);
    /// por eso toda conversión relanza. El único <c>catch</c> de la API es el de esta base. (Las clases de lógica solo pueden atrapar las excepciones del generador, que ocurren antes
    /// de la primera escritura: <c>GenerarFormatoLogica</c>.)</item>
    /// </list>
    /// </para>
    /// </summary>
    public abstract class BaseDeApi : IPlugin
    {
        /// <summary>
        /// El <c>uniquename</c> de la Custom API (<c>sanic_carp_capi_generarformato</c>…): lo que la plataforma pone en <c>MessageName</c>. Siempre el mismo valor: no es estado
        /// de la ejecución.
        /// </summary>
        internal abstract string NombreDeLaApi { get; }

        /// <summary>
        /// El <c>ExecutePrivilegeName</c> con que se registra la API (0.1): <c>prvReadsanic_carp_tbl_generacionformato</c>, <c>prvCreatesanic_carp_tbl_conversacion</c> o
        /// <c>prvWritesanic_carp_tbl_conversacion</c>. Es documental: lo hace cumplir la plataforma ANTES de ejecutar el plugin y el doble de pruebas no lo emula; existe para que una
        /// prueba lo contraste con <c>diseno/04-matriz-privilegios.md</c> y con el playbook.
        /// </summary>
        internal abstract string PrivilegioDeEjecucion { get; }

        /// <summary>El <c>-900</c> de esta API (<c>CodigosDeError.GEN_900</c>…): el error transitorio, con texto fijo y reintentable, en que se traduce toda falla de la plataforma (0.5).</summary>
        internal abstract DefinicionDeError ErrorTransitorio { get; }

        /// <summary>
        /// La regla de la API. Recibe el contexto de ESTA ejecución: lee los parámetros de entrada, delega en la clase de lógica y escribe TODOS los parámetros de salida al terminar
        /// bien (con el valor por defecto si no aplican). Rechaza lanzando <see cref="ErrorDeNegocio"/> y no atrapa fallas de escritura. No guarda nada en campos.
        /// </summary>
        internal abstract void Ejecutar(ContextoDeApi contexto);

        /// <summary>Punto de entrada de la plataforma. No se sobrescribe. Contrato en la descripción de la clase.</summary>
        public void Execute(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }
    }
}
