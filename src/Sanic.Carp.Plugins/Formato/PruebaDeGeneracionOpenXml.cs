using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Plantilla;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Adaptador del puerto <see cref="IPruebaDeGeneracion"/> sobre <see cref="GeneradorOpenXml"/> (diseno/03 2.8, Publicar, paso 3; DC-19; "Decisiones del arquitecto, 2026-10-19 (sublote 5e)").
    /// Vive en <c>Formato</c> porque es el único que conoce las excepciones del generador (<c>FormatoException</c> y subtipos), que el dominio no puede nombrar.
    /// <list type="bullet">
    /// <item><c>CalcularHuella</c>: <c>GeneradorFormato.CalcularHuella(contenido)</c>.</item>
    /// <item><c>Probar</c>: convierte cada fila con <c>ClavesDelGenerador.DeFila</c> (una opción o tipo desconocido es <c>MapeoInvalidoException</c>), arma <c>DatosDePrueba.Crear(capacidad)</c> y llama a
    /// <c>new GeneradorOpenXml(trazar).Generar(archivo, huella, mapeo, datos)</c>; descarta el libro generado (no lo guarda ni lo devuelve). <c>PlantillaInvalidaException</c> ⇒
    /// <see cref="ResultadoDePruebaDeGeneracion.DePlantillaInvalida"/>; <c>MapeoInvalidoException</c> y <c>DatosInvalidosException</c> ⇒ <see cref="ResultadoDePruebaDeGeneracion.DeFormatoInvalido"/> con
    /// <c>Message</c>. Cualquier otra excepción (<c>HuellaNoCoincideException</c>, <c>ArgumentOutOfRangeException</c> de una capacidad fuera de 1 a 60…) escapa tal cual. Los argumentos nulos lanzan
    /// <see cref="ArgumentNullException"/>.</item>
    /// </list>
    /// El delegado <c>trazar</c> es el de <c>ContextoDeStep.Trazar</c> (nulo permitido): el adaptador del generador lo usa para registrar la causa interna de una plantilla ilegible, sin datos del cliente.
    /// </summary>
    internal sealed class PruebaDeGeneracionOpenXml : IPruebaDeGeneracion
    {
        private readonly Action<string> _trazar;

        public PruebaDeGeneracionOpenXml(Action<string> trazar)
        {
            _trazar = trazar;
        }

        public string CalcularHuella(byte[] contenido)
        {
            if (contenido == null) throw new ArgumentNullException(nameof(contenido));
            return GeneradorFormato.CalcularHuella(contenido);
        }

        public ResultadoDePruebaDeGeneracion Probar(byte[] archivo, string huella, IReadOnlyList<FilaDeMapeo> mapeo, int capacidad)
        {
            if (archivo == null) throw new ArgumentNullException(nameof(archivo));
            if (huella == null) throw new ArgumentNullException(nameof(huella));
            if (mapeo == null) throw new ArgumentNullException(nameof(mapeo));

            // El adaptador es el unico que nombra las excepciones del generador: el dominio recibe un resultado neutro (2.8, Publicar, paso 3).
            // HuellaNoCoincide y las demas escapan: con la huella recien calculada serian un defecto.
            try
            {
                var filas = new List<MapeoCelda>(mapeo.Count);
                foreach (var fila in mapeo) filas.Add(ClavesDelGenerador.DeFila(fila));
                var datos = DatosDePrueba.Crear(capacidad);
                new GeneradorOpenXml(_trazar).Generar(archivo, huella, filas, datos);
                return ResultadoDePruebaDeGeneracion.Exito();
            }
            catch (PlantillaInvalidaException ex)
            {
                return ResultadoDePruebaDeGeneracion.DePlantillaInvalida(ex.Message);
            }
            catch (MapeoInvalidoException ex)
            {
                return ResultadoDePruebaDeGeneracion.DeFormatoInvalido(ex.Message);
            }
            catch (DatosInvalidosException ex)
            {
                return ResultadoDePruebaDeGeneracion.DeFormatoInvalido(ex.Message);
            }
        }
    }
}
