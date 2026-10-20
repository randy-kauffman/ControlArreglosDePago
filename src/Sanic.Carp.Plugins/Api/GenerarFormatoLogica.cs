using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Arreglo;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Formato;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// La lógica de <c>sanic_carp_capi_generarformato</c> (diseno/03 1.1 y "Decisiones del arquitecto, 2026-10-19 (lote 6)"). Solo conoce puertos: la API (<see cref="GenerarFormatoApi"/>)
    /// decide con qué servicio construyó cada uno. Sin campos mutables ni lectura del reloj fuera de <see cref="IReloj"/>.
    /// <para>
    /// <b>Qué servicio hace qué</b> (la API arma los puertos así; las pruebas lo miden con <c>Consultas</c> y <c>Escrituras</c> del doble): LLAMADOR: <paramref name="arreglosComoLlamador"/>,
    /// <paramref name="cuotas"/>, <paramref name="clientes"/>, <paramref name="usuarios"/> y <paramref name="accesos"/>. SYSTEM: <paramref name="arreglosComoSistema"/>,
    /// <paramref name="consecutivos"/>, <paramref name="versiones"/>, <paramref name="lecturasDeVersion"/> y la configuración. Hay UN solo servicio de cada tipo por ejecución.
    /// </para>
    /// <para>
    /// <b>Contrato de <see cref="Ejecutar"/>, en orden</b> (el primer rechazo gana; ninguna escritura antes del paso 11):
    /// <list type="number">
    /// <item><c>ArregloId</c> nulo ⇒ <c>CARP-GEN-001</c>. No toca ningún puerto.</item>
    /// <item><c>ahora</c> = <c>reloj.AhoraUtc</c>, una sola lectura.</item>
    /// <item>Arreglo: <c>arreglosComoLlamador.Leer(id, ArmadorDeDatosDelFormato.ColumnasDelArreglo)</c>. <c>null</c> (no existe O sin acceso) ⇒ <c>CARP-GEN-002</c>, el mismo texto (no revelar existencia).</item>
    /// <item>Cuotas: <c>cuotas.Listar(id, ColumnasDeLaCuota)</c> (ya ordenadas por <c>sanic_numero</c>). Deben ser exactamente <c>sanic_plazo</c> y numeradas 1..plazo; si no ⇒ <c>CARP-GEN-005</c> con {n} = cuántas
    /// hay y {plazo}. Un <c>sanic_plazo</c> ausente cuenta como 0.</item>
    /// <item>Cliente: congelado = <c>sanic_fechacongelamiento</c> presente. Si NO está congelado se lee <c>clientes.Leer(sanic_clienteid, ColumnasDelCliente)</c> con el llamador; un Cliente que no se puede leer (o un arreglo sin
    /// <c>sanic_clienteid</c>) ⇒ <c>CARP-GEN-002</c> (el diseño no lo dice; decisión del arquitecto: no se congela ni se imprime en blanco una instantánea que no se pudo leer). Congelado: no se lee el Cliente (PA-GEN-03).
    /// <c>oficial</c> = <c>usuarios.NombreCompleto(sanic_oficialid)</c> (llamador); ilegible o ausente ⇒ <c>null</c>, no es error.</item>
    /// <item>Versión a usar: SIN <c>VersionPreviaId</c>, la <c>sanic_versionplantillaid</c> del arreglo (cualquier estado: una Histórica es válida para un arreglo antiguo); un arreglo sin versión ⇒ <c>CARP-GEN-003</c>.
    /// CON <c>VersionPreviaId</c> (vista previa), en este orden: (a) <c>versiones.Leer(previa, [sanic_estado, sanic_huella, sanic_capacidad])</c> con SYSTEM; <c>null</c> ⇒ <c>CARP-GEN-007</c> (no existe y sin permiso dan el mismo
    /// mensaje); (b) <c>sanic_estado</c> distinto de Borrador ⇒ <c>CARP-GEN-006</c>; (c) <c>accesos.PuedeEscribir(sanic_carp_tbl_versionplantilla, previa, ActorId)</c> falso ⇒ <c>CARP-GEN-007</c>.</item>
    /// <item>Con SYSTEM: <c>versiones.Leer(version, [sanic_estado, sanic_huella, sanic_capacidad])</c> (si no se leyó en 6a; <c>null</c> ⇒ <c>CARP-GEN-003</c>); <c>lecturasDeVersion.ArchivoDeLaVersion(version)</c> (<c>null</c> ⇒ <c>CARP-GEN-003</c>);
    /// <c>lecturasDeVersion.FilasDeMapeoActivas(version)</c>.</item>
    /// <item><c>sanic_plazo</c> mayor que <c>sanic_capacidad</c> ⇒ <c>CARP-GEN-004</c> con {plazo} y {capacidad}.</item>
    /// <item>Número: Borrador con <c>sanic_numeroprovisional</c> guardado ⇒ ese; Borrador sin él ⇒ <c>ConsecutivoDeCredito.Siguiente(consecutivos.NumerosDelCredito(producto, numeroproducto))</c> (SYSTEM: cuenta TODOS los arreglos del
    /// crédito, también los que el llamador no lee; un siguiente mayor que 9999 lanza <c>CARP-ARR-023</c>); el resto de los estados ⇒ <c>sanic_numeroarreglo</c> (puede ser <c>null</c>). Datos: <see cref="ArmadorDeDatosDelFormato.Armar"/>.
    /// Huella esperada: la <c>sanic_huella</c> de la versión (ausente ⇒ cadena vacía, que no coincide y da <c>CARP-GEN-008</c>); en VISTA PREVIA, <c>generador.CalcularHuella(archivo)</c> (el borrador puede no tener huella).</item>
    /// <item><c>generador.Generar(archivo, huella, mapeo, datos)</c> con el mapeo convertido por <c>ClavesDelGenerador.DeFila</c>. El ÚNICO <c>catch</c> de esta clase: <c>HuellaNoCoincideException</c> ⇒ <c>CARP-GEN-008</c>;
    /// <c>PlantillaInvalidaException</c> ⇒ <c>-009</c>; <c>MapeoInvalidoException</c> ⇒ <c>-010</c> con {detalle} = sus errores unidos por <c>"; "</c>; <c>DatosInvalidosException</c> ⇒ <c>-011</c> con {detalle} = el mensaje. Ninguna lleva
    /// valores del cliente. Se traza el TIPO y nunca el mensaje de la causa (el adaptador ya trazó la causa interna). Es seguro atraparlas acá porque todavía no hubo ninguna escritura (0.5). Cualquier otra excepción sube sin atraparse.</item>
    /// <item>Si NO es vista previa, escribe con SYSTEM en este orden, sin atrapar nada: (11.0) <c>configuracion.TeamPropietarioId</c> (una lectura; un <c>CARP-CFG-*</c> sube sin haber escrito nada); (a) si el arreglo está ACTIVO
    /// (<c>statecode</c> 0) y no congelado: <c>arreglosComoSistema.Actualizar(id, …)</c> con <c>sanic_fechacongelamiento = ahora</c>, las cuatro <c>sanic_instantanea*</c> copiadas del Cliente (un dato <c>null</c> se escribe como
    /// <c>null</c>) y, SOLO si es Borrador, <c>sanic_numeroprovisional</c> = el número del paso 9; un arreglo inactivo (Terminado, Anulado, Descartado) o ya congelado NO se escribe (OA-9, PA-GEN-03, PA-GEN-22); (b)
    /// <c>arreglosComoSistema.RegistrarGeneracion(id, version, ActorId, ahora, team)</c>: la bitácora se escribe SIEMPRE (también con el arreglo inactivo y también si ya estaba congelado). El actor sale de <c>Pedido.ActorId</c>, nunca de la identidad del servicio.</item>
    /// <item>Resultado: <c>Archivo</c> = Base64 del libro; <c>NombreDelArchivo</c> = <c>"F-NIC-0000052-" + sanic_nombre + ".xlsx"</c>; <c>NumeroProvisional</c> = el número del paso 9 si es Borrador, 0 si no; <c>Congelado</c> = estaba congelado o
    /// esta llamada lo congeló (en vista previa, el estado que ya tenía); <c>Advertencias</c> = las del generador unidas por <c>"\n"</c> (vacío si no hay).</item>
    /// </list>
    /// Toda la llamada es UNA transacción: el archivo se genera antes de escribir y una falla de la plataforma (<c>FaultException</c>) NO se atrapa acá: la base la vuelve <c>CARP-GEN-900</c> y la transacción revierte (PA-GEN-23). La
    /// vista previa no escribe nada, ni siquiera lee la configuración (DC-18). Traza solo ids y códigos.
    /// </para>
    /// </summary>
    internal sealed class GenerarFormatoLogica
    {
        private const string ColNombre = "sanic_nombre";
        private const string ColEstado = "statecode";
        private const string ColMotivo = "statuscode";
        private const string ColCliente = "sanic_clienteid";
        private const string ColOficial = "sanic_oficialid";
        private const string ColProducto = "sanic_producto";
        private const string ColNumeroProducto = "sanic_numeroproducto";
        private const string ColNumeroArreglo = "sanic_numeroarreglo";
        private const string ColProvisional = "sanic_numeroprovisional";
        private const string ColPlazo = "sanic_plazo";
        private const string ColCuenta = "sanic_cuentadebitar";
        private const string ColCongelamiento = "sanic_fechacongelamiento";
        private const string ColInstantaneaCedula = "sanic_instantaneacedula";
        private const string ColVersion = "sanic_versionplantillaid";
        private const string ColCedulaDelCliente = "sanic_cedula";
        private const string ColHuella = "sanic_huella";
        private const string ColCapacidad = "sanic_capacidad";
        private const string ColEstadoDeVersion = "sanic_estado";

        // De solo lectura: sin estado estático mutable (guardia PA6_GRD_02).
        private static readonly IReadOnlyList<string> ColumnasDeLaVersion = new ReadOnlyCollection<string>(new[] { ColEstadoDeVersion, ColHuella, ColCapacidad });
        private static readonly IReadOnlyList<string> SoloLaCedulaDelCliente = new ReadOnlyCollection<string>(new[] { ColCedulaDelCliente });

        private readonly IArreglos _arreglosComoLlamador;
        private readonly IArreglos _arreglosComoSistema;
        private readonly ICuotas _cuotas;
        private readonly IClientes _clientes;
        private readonly IClientes _clientesComoSistema;
        private readonly IUsuarios _usuarios;
        private readonly IConsecutivos _consecutivos;
        private readonly IVersiones _versiones;
        private readonly ILecturasDeVersion _lecturasDeVersion;
        private readonly IAccesosDeFila _accesos;
        private readonly IGeneradorDeFormato _generador;
        private readonly IConfiguracionDelArea _configuracion;
        private readonly IReloj _reloj;
        private readonly Action<string> _trazar;

        /// <summary>
        /// Cualquier argumento nulo ⇒ <see cref="ArgumentNullException"/> (<paramref name="trazar"/> y <paramref name="clientesComoSistema"/> pueden ser <c>null</c>). No consulta nada al construirse.
        /// <paramref name="clientesComoSistema"/> sirve SOLO para decidir <c>CARP-GEN-012</c> (decisión 8(c)): lee la cédula del Cliente con SYSTEM y no devuelve su valor a quien llama. La API lo
        /// arma con <c>new ClientesDataverse(sistema, sistema)</c>; si llega <c>null</c> (las pruebas de la lógica con 13 argumentos) no se puede comprobar la cédula del Cliente y esa
        /// comprobación se omite; la de la cuenta y la de la instantánea usan <paramref name="arreglosComoSistema"/> y siempre se hacen.
        /// </summary>
        public GenerarFormatoLogica(IArreglos arreglosComoLlamador, IArreglos arreglosComoSistema, ICuotas cuotas, IClientes clientes, IUsuarios usuarios, IConsecutivos consecutivos,
            IVersiones versiones, ILecturasDeVersion lecturasDeVersion, IAccesosDeFila accesos, IGeneradorDeFormato generador, IConfiguracionDelArea configuracion, IReloj reloj,
            Action<string> trazar, IClientes clientesComoSistema = null)
        {
            if (arreglosComoLlamador == null) throw new ArgumentNullException(nameof(arreglosComoLlamador));
            if (arreglosComoSistema == null) throw new ArgumentNullException(nameof(arreglosComoSistema));
            if (cuotas == null) throw new ArgumentNullException(nameof(cuotas));
            if (clientes == null) throw new ArgumentNullException(nameof(clientes));
            if (usuarios == null) throw new ArgumentNullException(nameof(usuarios));
            if (consecutivos == null) throw new ArgumentNullException(nameof(consecutivos));
            if (versiones == null) throw new ArgumentNullException(nameof(versiones));
            if (lecturasDeVersion == null) throw new ArgumentNullException(nameof(lecturasDeVersion));
            if (accesos == null) throw new ArgumentNullException(nameof(accesos));
            if (generador == null) throw new ArgumentNullException(nameof(generador));
            if (configuracion == null) throw new ArgumentNullException(nameof(configuracion));
            if (reloj == null) throw new ArgumentNullException(nameof(reloj));
            _arreglosComoLlamador = arreglosComoLlamador;
            _arreglosComoSistema = arreglosComoSistema;
            _cuotas = cuotas;
            _clientes = clientes;
            _clientesComoSistema = clientesComoSistema;
            _usuarios = usuarios;
            _consecutivos = consecutivos;
            _versiones = versiones;
            _lecturasDeVersion = lecturasDeVersion;
            _accesos = accesos;
            _generador = generador;
            _configuracion = configuracion;
            _reloj = reloj;
            _trazar = trazar;
        }

        /// <summary>Contrato en la descripción de la clase. Rechaza lanzando <see cref="Dominio.Errores.ErrorDeNegocio"/>; <paramref name="pedido"/> nulo ⇒ <see cref="ArgumentNullException"/>.</summary>
        public ResultadoDeGenerarFormato Ejecutar(PedidoDeGenerarFormato pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));

            // Paso 1: sin arreglo no se toca ningún puerto.
            if (!pedido.ArregloId.HasValue) throw CodigosDeError.GEN_001.Crear();
            var arregloId = pedido.ArregloId.Value;
            var vistaPrevia = pedido.VersionPreviaId.HasValue;

            // Paso 2: una sola lectura del reloj.
            var ahora = _reloj.AhoraUtc;
            Trazar("generarformato arreglo=" + arregloId.ToString("D") + " vistaPrevia=" + (vistaPrevia ? "si" : "no"));

            // Paso 3: el mismo rechazo para "no existe" y "sin acceso" (no se revela la existencia).
            var arreglo = _arreglosComoLlamador.Leer(arregloId, ArmadorDeDatosDelFormato.ColumnasDelArreglo);
            if (arreglo == null) throw CodigosDeError.GEN_002.Crear();

            // Paso 4: el calendario completo, numerado 1..plazo.
            var plazo = Entero(arreglo, ColPlazo) ?? 0;
            var filasDeCuotas = _cuotas.Listar(arregloId, ArmadorDeDatosDelFormato.ColumnasDeLaCuota);
            if (!CalendarioCompleto(filasDeCuotas, plazo)) throw CodigosDeError.GEN_005.Crear(filasDeCuotas.Count, plazo);

            // Paso 5: un arreglo congelado imprime su instantánea y no mira al Cliente (PA-GEN-03); si no, el Cliente debe poder leerse (decisión 4(a)).
            var congelado = arreglo.Valores.ContainsKey(ColCongelamiento);
            FilaDeDatos cliente = null;
            Guid? clienteId = null;
            if (!congelado)
            {
                var referencia = Valor(arreglo, ColCliente) as ReferenciaDeEntidad;
                if (referencia == null) throw CodigosDeError.GEN_002.Crear();
                cliente = _clientes.Leer(referencia.Id, ArmadorDeDatosDelFormato.ColumnasDelCliente);
                if (cliente == null) throw CodigosDeError.GEN_002.Crear();
                clienteId = referencia.Id;
            }
            var datosDelCliente = ArmadorDeDatosDelFormato.DatosDelCliente(arreglo, cliente);
            var referenciaDelOficial = Valor(arreglo, ColOficial) as ReferenciaDeEntidad;
            var oficial = referenciaDelOficial == null ? null : _usuarios.NombreCompleto(referenciaDelOficial.Id);

            // Decisión 8(c): un formato del banco nunca sale sin la cédula ni la cuenta por un problema de permisos.
            ExigirElPerfilDeDatosSensibles(arregloId, arreglo, congelado, clienteId, cliente);

            // Pasos 6 y 7: la versión a usar, su archivo y su mapeo (SYSTEM).
            Guid versionId;
            FilaDeDatos version;
            if (vistaPrevia)
            {
                versionId = pedido.VersionPreviaId.Value;
                version = _versiones.Leer(versionId, ColumnasDeLaVersion);
                if (version == null) throw CodigosDeError.GEN_007.Crear();
                if (EstadoDeLaVersion(version) != EstadoDeVersionDePlantilla.Borrador) throw CodigosDeError.GEN_006.Crear();
                if (!_accesos.PuedeEscribir(TablasDelArea.VersionDePlantilla, versionId, pedido.ActorId)) throw CodigosDeError.GEN_007.Crear();
            }
            else
            {
                var referenciaDeVersion = Valor(arreglo, ColVersion) as ReferenciaDeEntidad;
                if (referenciaDeVersion == null) throw CodigosDeError.GEN_003.Crear();
                versionId = referenciaDeVersion.Id;
                version = _versiones.Leer(versionId, ColumnasDeLaVersion);
                if (version == null) throw CodigosDeError.GEN_003.Crear();
            }
            var archivo = _lecturasDeVersion.ArchivoDeLaVersion(versionId);
            if (archivo == null) throw CodigosDeError.GEN_003.Crear();
            var filasDeMapeo = _lecturasDeVersion.FilasDeMapeoActivas(versionId);

            // Paso 8.
            var capacidad = Entero(version, ColCapacidad) ?? 0;
            if (plazo > capacidad) throw CodigosDeError.GEN_004.Crear(plazo, capacidad);

            // Paso 9: el número impreso y los datos.
            var estado = EstadoDelArreglo(arreglo);
            var esBorrador = estado == EstadoDeArreglo.Borrador;
            int? numeroProvisional = null;
            int? numeroImpreso;
            if (esBorrador)
            {
                numeroProvisional = Entero(arreglo, ColProvisional);
                if (!numeroProvisional.HasValue) numeroProvisional = SiguienteDelCredito(arreglo);
                numeroImpreso = numeroProvisional;
            }
            else
            {
                numeroImpreso = Entero(arreglo, ColNumeroArreglo);
            }
            var datos = ArmadorDeDatosDelFormato.Armar(arreglo, filasDeCuotas, datosDelCliente, oficial, numeroImpreso);
            var huella = vistaPrevia ? _generador.CalcularHuella(archivo) : (Valor(version, ColHuella) as string ?? string.Empty);

            // Paso 10: único catch de la clase. Es seguro porque todavía no hubo ninguna escritura (0.5).
            ResultadoGeneracion generado;
            try
            {
                var mapeo = new List<MapeoCelda>(filasDeMapeo.Count);
                foreach (var fila in filasDeMapeo) mapeo.Add(ClavesDelGenerador.DeFila(fila));
                generado = _generador.Generar(archivo, huella, mapeo, datos);
            }
            catch (HuellaNoCoincideException e)
            {
                Trazar("generarformato rechazo por " + e.GetType().Name);
                throw CodigosDeError.GEN_008.Crear();
            }
            catch (PlantillaInvalidaException e)
            {
                Trazar("generarformato rechazo por " + e.GetType().Name);
                throw CodigosDeError.GEN_009.Crear();
            }
            catch (MapeoInvalidoException e)
            {
                Trazar("generarformato rechazo por " + e.GetType().Name);
                throw CodigosDeError.GEN_010.Crear(string.Join("; ", e.Errores));
            }
            catch (DatosInvalidosException e)
            {
                Trazar("generarformato rechazo por " + e.GetType().Name);
                throw CodigosDeError.GEN_011.Crear(e.Message);
            }

            // Paso 11: desde acá solo hay fallas de la plataforma, que suben sin atraparse.
            var congeladoAlTerminar = congelado;
            if (!vistaPrevia)
            {
                var team = _configuracion.TeamPropietarioId;
                var activo = EstadoActivo(arreglo);
                if (activo && !congelado)
                {
                    var cambios = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { ColCongelamiento, ahora },
                        { "sanic_instantaneanombre", datosDelCliente.Nombre },
                        { ColInstantaneaCedula, datosDelCliente.Cedula },
                        { "sanic_instantaneacorreo", datosDelCliente.Correo },
                        { "sanic_instantaneacelular", datosDelCliente.Celular }
                    };
                    // El provisional solo se guarda al congelar un Borrador (decisión 4(e)); el de un Vigente reabierto no se toca (PA6_GEN_12).
                    if (esBorrador) cambios[ColProvisional] = numeroProvisional.Value;
                    _arreglosComoSistema.Actualizar(arregloId, cambios);
                    congeladoAlTerminar = true;
                }
                // La bitácora se escribe siempre, también con el arreglo inactivo o ya congelado; el actor es el del pedido, no la identidad del servicio.
                _arreglosComoSistema.RegistrarGeneracion(arregloId, versionId, pedido.ActorId, ahora, team);
            }
            Trazar("generarformato terminado arreglo=" + arregloId.ToString("D") + " congelado=" + (congeladoAlTerminar ? "si" : "no"));

            var nombre = Valor(arreglo, ColNombre) as string;
            if (nombre == null) throw new InvalidOperationException("El arreglo no trae la columna '" + ColNombre + "'.");
            var advertencias = generado.Advertencias == null ? string.Empty : string.Join("\n", generado.Advertencias);
            return new ResultadoDeGenerarFormato(
                Convert.ToBase64String(generado.Archivo), "F-NIC-0000052-" + nombre + ".xlsx", esBorrador ? numeroProvisional.Value : 0, congeladoAlTerminar, advertencias);
        }

        // ---- piezas ----

        private void Trazar(string texto)
        {
            if (_trazar != null) _trazar(texto);
        }

        private static object Valor(FilaDeDatos fila, string columna)
        {
            object valor;
            return fila.Valores.TryGetValue(columna, out valor) ? valor : null;
        }

        private static int? Entero(FilaDeDatos fila, string columna)
        {
            var valor = Valor(fila, columna);
            return valor is int ? (int?)(int)valor : null;
        }

        private static bool CalendarioCompleto(IReadOnlyList<FilaDeDatos> cuotas, int plazo)
        {
            if (cuotas.Count != plazo) return false;
            for (int i = 0; i < cuotas.Count; i++)
            {
                var numero = Entero(cuotas[i], "sanic_numero");
                if (!numero.HasValue || numero.Value != i + 1) return false;
            }
            return true;
        }

        private static EstadoDeVersionDePlantilla? EstadoDeLaVersion(FilaDeDatos version)
        {
            var opcion = Valor(version, ColEstadoDeVersion) as ValorDeOpcion;
            if (opcion == null || !Enum.IsDefined(typeof(EstadoDeVersionDePlantilla), opcion.Valor)) return null;
            return (EstadoDeVersionDePlantilla)opcion.Valor;
        }

        private static EstadoDeArreglo EstadoDelArreglo(FilaDeDatos arreglo)
        {
            var estado = Valor(arreglo, ColEstado) as ValorDeOpcion;
            var motivo = Valor(arreglo, ColMotivo) as ValorDeOpcion;
            if (estado == null || motivo == null) throw new InvalidOperationException("El arreglo no trae su estado.");
            var efectivo = Estados.DeArreglo(new EstadoEfectivo(estado.Valor, motivo.Valor));
            if (!efectivo.HasValue) throw new InvalidOperationException("El arreglo tiene un par de estado desconocido.");
            return efectivo.Value;
        }

        private static bool EstadoActivo(FilaDeDatos arreglo)
        {
            var estado = Valor(arreglo, ColEstado) as ValorDeOpcion;
            return estado != null && estado.Valor == Estados.Activo;
        }

        private int SiguienteDelCredito(FilaDeDatos arreglo)
        {
            var producto = Valor(arreglo, ColProducto) as ValorDeOpcion;
            var numeroDeProducto = Valor(arreglo, ColNumeroProducto) as string;
            if (producto == null || string.IsNullOrEmpty(numeroDeProducto)) throw new InvalidOperationException("El arreglo no trae su crédito.");
            // SYSTEM: cuenta TODOS los arreglos del crédito, también los que el llamador no lee.
            var existentes = _consecutivos.NumerosDelCredito((ProductoCrediticio)producto.Valor, numeroDeProducto);
            var siguiente = ConsecutivoDeCredito.Siguiente(existentes.Select(n => (int?)n));
            if (!siguiente.Ok) throw siguiente.Error;
            return siguiente.Numero;
        }

        /// <summary>
        /// Decisión 8(c). Sin el perfil de columna, la lectura del llamador trae AUSENTES la cuenta, la cédula de la instantánea y la cédula del Cliente (H-4). Si falta alguna de las que el
        /// formato va a imprimir, SYSTEM lee SOLO esas columnas para distinguir "sin permiso" de "vacía en la fila"; sus valores no salen de este método.
        /// </summary>
        private void ExigirElPerfilDeDatosSensibles(Guid arregloId, FilaDeDatos arreglo, bool congelado, Guid? clienteId, FilaDeDatos cliente)
        {
            var faltantes = new List<string>();
            if (!arreglo.Valores.ContainsKey(ColCuenta)) faltantes.Add(ColCuenta);
            if (congelado && !arreglo.Valores.ContainsKey(ColInstantaneaCedula)) faltantes.Add(ColInstantaneaCedula);
            if (faltantes.Count > 0)
            {
                var deSistema = _arreglosComoSistema.Leer(arregloId, faltantes);
                if (deSistema != null && faltantes.Any(c => deSistema.Valores.ContainsKey(c))) throw CodigosDeError.GEN_012.Crear();
            }

            if (cliente != null && clienteId.HasValue && _clientesComoSistema != null && !cliente.Valores.ContainsKey(ColCedulaDelCliente))
            {
                var deSistema = _clientesComoSistema.Leer(clienteId.Value, SoloLaCedulaDelCliente);
                if (deSistema != null && deSistema.Valores.ContainsKey(ColCedulaDelCliente)) throw CodigosDeError.GEN_012.Crear();
            }
        }
    }
}
