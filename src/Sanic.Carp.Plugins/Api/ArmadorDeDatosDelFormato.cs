using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Formato;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// Pieza pura de <c>generarformato</c> (diseno/03 1.1 "Datos del generador" y "Decisiones del arquitecto, 2026-10-19 (lote 6)"): arma el <see cref="DatosArreglo"/> que recibe el
    /// generador a partir de filas ya leídas. Sin lecturas, sin escrituras y sin reloj: se prueba sin almacén.
    /// </summary>
    internal static class ArmadorDeDatosDelFormato
    {
        /// <summary>
        /// Las columnas del arreglo que la API lee con el servicio del llamador (paso 3), sin repetir y en este orden: <c>sanic_nombre</c>, <c>statecode</c>, <c>statuscode</c>,
        /// <c>sanic_clienteid</c>, <c>sanic_oficialid</c>, <c>sanic_producto</c>, <c>sanic_numeroproducto</c>, <c>sanic_tipo</c>, <c>sanic_moneda</c>, <c>sanic_numeroarreglo</c>,
        /// <c>sanic_numeroprovisional</c>, <c>sanic_saldoinicial</c>, <c>sanic_montoabonado</c>, <c>sanic_saldoremanente</c>, <c>sanic_cuotamensual</c>, <c>sanic_totalpagar</c>,
        /// <c>sanic_plazo</c>, <c>sanic_fechainicio</c>, <c>sanic_fechafin</c>, <c>sanic_fechaformalizacion</c>, <c>sanic_cuentadebitar</c>, <c>sanic_comentarios</c>,
        /// <c>sanic_fechacongelamiento</c>, <c>sanic_instantaneanombre</c>, <c>sanic_instantaneacedula</c>, <c>sanic_instantaneacorreo</c>, <c>sanic_instantaneacelular</c> y
        /// <c>sanic_versionplantillaid</c>. Nunca <c>ColumnSet(true)</c>.
        /// </summary>
        public static IReadOnlyList<string> ColumnasDelArreglo { get { return _columnasDelArreglo; } }

        // De solo lectura: sin estado mutable en el ensamblado del plugin (reglas de plugin).
        private static readonly IReadOnlyList<string> _columnasDelArreglo = new ReadOnlyCollection<string>(new[]
        {
            "sanic_nombre", "statecode", "statuscode", "sanic_clienteid", "sanic_oficialid", "sanic_producto", "sanic_numeroproducto", "sanic_tipo", "sanic_moneda", "sanic_numeroarreglo",
            "sanic_numeroprovisional", "sanic_saldoinicial", "sanic_montoabonado", "sanic_saldoremanente", "sanic_cuotamensual", "sanic_totalpagar", "sanic_plazo", "sanic_fechainicio",
            "sanic_fechafin", "sanic_fechaformalizacion", "sanic_cuentadebitar", "sanic_comentarios", "sanic_fechacongelamiento", "sanic_instantaneanombre", "sanic_instantaneacedula",
            "sanic_instantaneacorreo", "sanic_instantaneacelular", "sanic_versionplantillaid"
        });

        private static readonly IReadOnlyList<string> _columnasDeLaCuota = new ReadOnlyCollection<string>(new[]
        {
            "sanic_numero", "sanic_fechavencimiento", "sanic_monto", "sanic_fechapago", "sanic_montopagado"
        });

        private static readonly IReadOnlyList<string> _columnasDelCliente = new ReadOnlyCollection<string>(new[]
        {
            "sanic_nombrecompleto", "sanic_cedula", "sanic_correo", "sanic_celular"
        });

        /// <summary>Las columnas de cada cuota (paso 4), en este orden: <c>sanic_numero</c>, <c>sanic_fechavencimiento</c>, <c>sanic_monto</c>, <c>sanic_fechapago</c>, <c>sanic_montopagado</c>.</summary>
        public static IReadOnlyList<string> ColumnasDeLaCuota { get { return _columnasDeLaCuota; } }

        /// <summary>Las columnas del Cliente que se leen con el llamador (paso 5): <c>sanic_nombrecompleto</c>, <c>sanic_cedula</c>, <c>sanic_correo</c>, <c>sanic_celular</c>.</summary>
        public static IReadOnlyList<string> ColumnasDelCliente { get { return _columnasDelCliente; } }

        /// <summary>
        /// Los datos del cliente que el formato imprime. Si el arreglo está congelado (<c>sanic_fechacongelamiento</c> presente en <paramref name="arreglo"/>), la INSTANTÁNEA del propio
        /// arreglo: <c>sanic_instantaneanombre</c>, <c>…cedula</c>, <c>…correo</c> y <c>…celular</c>; el Cliente no se mira (PA-GEN-03). Si no, los cuatro del <paramref name="cliente"/>
        /// leído (<c>sanic_nombrecompleto</c>, <c>sanic_cedula</c>, <c>sanic_correo</c>, <c>sanic_celular</c>), y el cliente nulo ⇒ <see cref="ArgumentNullException"/> (la API ya lo trató antes
        /// con <c>CARP-GEN-002</c>). Una columna ausente o vacía da <c>null</c>. <paramref name="arreglo"/> nulo ⇒ <see cref="ArgumentNullException"/>.
        /// </summary>
        public static DatosDelClienteParaElFormato DatosDelCliente(FilaDeDatos arreglo, FilaDeDatos cliente)
        {
            if (arreglo == null) throw new ArgumentNullException(nameof(arreglo));

            if (arreglo.Valores.ContainsKey("sanic_fechacongelamiento"))
            {
                return new DatosDelClienteParaElFormato(
                    Texto(arreglo, "sanic_instantaneanombre"), Texto(arreglo, "sanic_instantaneacedula"),
                    Texto(arreglo, "sanic_instantaneacorreo"), Texto(arreglo, "sanic_instantaneacelular"));
            }

            if (cliente == null) throw new ArgumentNullException(nameof(cliente));
            return new DatosDelClienteParaElFormato(
                Texto(cliente, "sanic_nombrecompleto"), Texto(cliente, "sanic_cedula"), Texto(cliente, "sanic_correo"), Texto(cliente, "sanic_celular"));
        }

        /// <summary>
        /// Arma los datos del generador (1.1 "Datos del generador"): UNA clave por cada una de las 20 opciones de valor simple de <see cref="ClavesDelGenerador"/> (aunque el valor sea
        /// <c>null</c>: el generador las exige, spike 1 hallazgo 3), la lista <c>Cuotas</c> ordenada por número y <c>Plazo</c> = <c>sanic_plazo</c>. Orígenes:
        /// <list type="bullet">
        /// <item><c>fechaFormalizacion</c>, <c>fechaInicio</c>, <c>fechaFin</c>: las <c>DateTime</c> TAL CUAL están guardadas, sin conversión de zona (0.6, PA-GEN-21); <c>null</c> si faltan.</item>
        /// <item><c>oficial</c>: <paramref name="oficial"/> (<c>fullname</c>, o <c>null</c>); <c>clienteNombre</c>, <c>clienteCedula</c>, <c>clienteCorreo</c>, <c>clienteCelular</c>: <paramref name="cliente"/>.</item>
        /// <item><c>numeroProducto</c>, <c>cuentaDebitar</c> y <c>comentarios</c>: el texto de la columna, <c>null</c> si falta o está vacío (PA-GEN-20).</item>
        /// <item><c>tipo</c> y <c>moneda</c>: <see cref="ClavesDelGenerador.TextoFormal(TipoDeArreglo)"/> y <see cref="ClavesDelGenerador.TextoFormal(Moneda)"/> (OA-7, PA-GEN-19); una opción ausente o no definida ⇒ <c>null</c>.</item>
        /// <item><c>numeroArreglo</c>: <paramref name="numeroDeArreglo"/> (el provisional en un Borrador, <c>sanic_numeroarreglo</c> en los demás; <c>null</c> si no hay).</item>
        /// <item><c>saldoInicial</c>, <c>montoAbonado</c>, <c>saldoRemanente</c>, <c>cuota</c>, <c>totalPagar</c>: <c>decimal</c> de las columnas (<c>sanic_cuotamensual</c> → <c>cuota</c>); <c>plazo</c>: <c>int</c>.</item>
        /// <item>Cada cuota: <see cref="CuotaDato"/> con número, vencimiento, monto, fecha de pago y monto pagado (los dos últimos <c>null</c> si no hay pago), ordenadas por número.</item>
        /// </list>
        /// Un valor que no es del tipo esperado, o <paramref name="arreglo"/>, <paramref name="cuotas"/> o <paramref name="cliente"/> nulos ⇒ <see cref="ArgumentNullException"/> /
        /// <see cref="InvalidOperationException"/>. No consulta nada ni lee el reloj.
        /// </summary>
        public static DatosArreglo Armar(FilaDeDatos arreglo, IReadOnlyList<FilaDeDatos> cuotas, DatosDelClienteParaElFormato cliente, string oficial, int? numeroDeArreglo)
        {
            if (arreglo == null) throw new ArgumentNullException(nameof(arreglo));
            if (cuotas == null) throw new ArgumentNullException(nameof(cuotas));
            if (cliente == null) throw new ArgumentNullException(nameof(cliente));

            var plazo = EnteroRequerido(arreglo, "sanic_plazo");
            var valores = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ClavesDelGenerador.Clave(CampoMapeable.FechaDeFormalizacion), FechaOpcional(arreglo, "sanic_fechaformalizacion") },
                { ClavesDelGenerador.Clave(CampoMapeable.OficialDeArreglo), oficial },
                { ClavesDelGenerador.Clave(CampoMapeable.NombreDelCliente), cliente.Nombre },
                { ClavesDelGenerador.Clave(CampoMapeable.CedulaDelCliente), cliente.Cedula },
                { ClavesDelGenerador.Clave(CampoMapeable.CorreoDelCliente), cliente.Correo },
                { ClavesDelGenerador.Clave(CampoMapeable.CelularDelCliente), cliente.Celular },
                { ClavesDelGenerador.Clave(CampoMapeable.NumeroDePrestamoOTarjeta), Texto(arreglo, "sanic_numeroproducto") },
                { ClavesDelGenerador.Clave(CampoMapeable.TipoDeArreglo), TextoDelTipo(arreglo) },
                { ClavesDelGenerador.Clave(CampoMapeable.NumeroDeArreglo), numeroDeArreglo.HasValue ? (object)numeroDeArreglo.Value : null },
                { ClavesDelGenerador.Clave(CampoMapeable.SaldoInicial), DecimalRequerido(arreglo, "sanic_saldoinicial") },
                { ClavesDelGenerador.Clave(CampoMapeable.MontoAbonado), DecimalRequerido(arreglo, "sanic_montoabonado") },
                { ClavesDelGenerador.Clave(CampoMapeable.SaldoRemanente), DecimalRequerido(arreglo, "sanic_saldoremanente") },
                { ClavesDelGenerador.Clave(CampoMapeable.PlazoEnCuotas), plazo },
                { ClavesDelGenerador.Clave(CampoMapeable.CuotaMensual), DecimalRequerido(arreglo, "sanic_cuotamensual") },
                { ClavesDelGenerador.Clave(CampoMapeable.TotalAPagar), DecimalRequerido(arreglo, "sanic_totalpagar") },
                { ClavesDelGenerador.Clave(CampoMapeable.FechaDeInicio), FechaOpcional(arreglo, "sanic_fechainicio") },
                { ClavesDelGenerador.Clave(CampoMapeable.FechaDeFinalizacion), FechaOpcional(arreglo, "sanic_fechafin") },
                { ClavesDelGenerador.Clave(CampoMapeable.CuentaADebitar), Texto(arreglo, "sanic_cuentadebitar") },
                { ClavesDelGenerador.Clave(CampoMapeable.Comentarios), Texto(arreglo, "sanic_comentarios") },
                { ClavesDelGenerador.Clave(CampoMapeable.Moneda), TextoDeLaMoneda(arreglo) }
            };

            var lista = new List<CuotaDato>(cuotas.Count);
            foreach (var cuota in cuotas)
            {
                if (cuota == null) throw new ArgumentNullException(nameof(cuotas), "Una cuota es nula.");
                lista.Add(new CuotaDato(
                    EnteroRequerido(cuota, "sanic_numero"), FechaRequerida(cuota, "sanic_fechavencimiento"), DecimalRequerido(cuota, "sanic_monto"),
                    FechaOpcional(cuota, "sanic_fechapago"), DecimalOpcional(cuota, "sanic_montopagado")));
            }
            lista.Sort((a, b) => a.Numero.CompareTo(b.Numero));

            return new DatosArreglo(new ReadOnlyDictionary<string, object>(valores), plazo, lista);
        }

        // ---- lectura tipada de filas: un tipo equivocado es un defecto, no un rechazo de negocio ----

        private static string Texto(FilaDeDatos fila, string columna)
        {
            object valor;
            if (!fila.Valores.TryGetValue(columna, out valor) || valor == null) return null;
            var texto = valor as string;
            if (texto == null) throw Equivocado(columna, "un texto");
            return texto.Length == 0 ? null : texto;
        }

        private static DateTime? FechaOpcional(FilaDeDatos fila, string columna)
        {
            object valor;
            if (!fila.Valores.TryGetValue(columna, out valor) || valor == null) return null;
            if (!(valor is DateTime)) throw Equivocado(columna, "una fecha");
            return (DateTime)valor;
        }

        private static DateTime FechaRequerida(FilaDeDatos fila, string columna)
        {
            var fecha = FechaOpcional(fila, columna);
            if (!fecha.HasValue) throw Equivocado(columna, "una fecha presente");
            return fecha.Value;
        }

        private static decimal? DecimalOpcional(FilaDeDatos fila, string columna)
        {
            object valor;
            if (!fila.Valores.TryGetValue(columna, out valor) || valor == null) return null;
            if (valor is decimal) return (decimal)valor;
            var dinero = valor as ValorDeDinero;
            if (dinero != null) return dinero.Valor;
            throw Equivocado(columna, "un decimal");
        }

        private static decimal DecimalRequerido(FilaDeDatos fila, string columna)
        {
            var valor = DecimalOpcional(fila, columna);
            if (!valor.HasValue) throw Equivocado(columna, "un decimal presente");
            return valor.Value;
        }

        private static int EnteroRequerido(FilaDeDatos fila, string columna)
        {
            object valor;
            if (!fila.Valores.TryGetValue(columna, out valor) || !(valor is int)) throw Equivocado(columna, "un entero presente");
            return (int)valor;
        }

        private static int? OpcionOpcional(FilaDeDatos fila, string columna)
        {
            object valor;
            if (!fila.Valores.TryGetValue(columna, out valor) || valor == null) return null;
            var opcion = valor as ValorDeOpcion;
            if (opcion == null) throw Equivocado(columna, "una opción");
            return opcion.Valor;
        }

        private static string TextoDelTipo(FilaDeDatos arreglo)
        {
            var opcion = OpcionOpcional(arreglo, "sanic_tipo");
            if (!opcion.HasValue) return null;
            var tipo = (TipoDeArreglo)opcion.Value;
            return Enum.IsDefined(typeof(TipoDeArreglo), tipo) ? ClavesDelGenerador.TextoFormal(tipo) : null;
        }

        private static string TextoDeLaMoneda(FilaDeDatos arreglo)
        {
            var opcion = OpcionOpcional(arreglo, "sanic_moneda");
            if (!opcion.HasValue) return null;
            var moneda = (Moneda)opcion.Value;
            return Enum.IsDefined(typeof(Moneda), moneda) ? ClavesDelGenerador.TextoFormal(moneda) : null;
        }

        private static InvalidOperationException Equivocado(string columna, string esperado)
        {
            // Solo el nombre de la columna: nunca el valor, que puede ser un dato del cliente.
            return new InvalidOperationException("La columna '" + columna + "' no trae " + esperado + ".");
        }
    }
}
