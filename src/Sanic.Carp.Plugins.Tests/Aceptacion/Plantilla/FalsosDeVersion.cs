using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Plantilla;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioVersion
{
    /// <summary>
    /// <see cref="ILecturasDeVersion"/> falso de las pruebas de Versión de plantilla (sublote 5e, vuelta 2A): devuelve lo que se le arma, cuenta cada pedido (con el id) y puede lanzar en cada lectura.
    /// El estado, el archivo y las filas se arman UNA vez por prueba y se devuelven tal cual (la misma instancia).
    /// </summary>
    internal sealed class LecturasDeVersionContadoras : ILecturasDeVersion
    {
        public EstadoDeVersionDePlantilla? Estado { get; set; } = EstadoDeVersionDePlantilla.Borrador;
        public byte[] Archivo { get; set; }
        public IReadOnlyList<FilaDeMapeo> Filas { get; set; }

        public Exception FallaAlLeerElEstado { get; set; }
        public Exception FallaAlLeerElArchivo { get; set; }
        public Exception FallaAlLeerElMapeo { get; set; }

        public List<Guid> EstadosPedidos { get; } = new List<Guid>();
        public List<Guid> ArchivosPedidos { get; } = new List<Guid>();
        public List<Guid> MapeosPedidos { get; } = new List<Guid>();

        public EstadoDeVersionDePlantilla? EstadoDeLaVersion(Guid versionId)
        {
            EstadosPedidos.Add(versionId);
            if (FallaAlLeerElEstado != null) throw FallaAlLeerElEstado;
            return Estado;
        }

        public byte[] ArchivoDeLaVersion(Guid versionId)
        {
            ArchivosPedidos.Add(versionId);
            if (FallaAlLeerElArchivo != null) throw FallaAlLeerElArchivo;
            return Archivo;
        }

        public IReadOnlyList<FilaDeMapeo> FilasDeMapeoActivas(Guid versionId)
        {
            MapeosPedidos.Add(versionId);
            if (FallaAlLeerElMapeo != null) throw FallaAlLeerElMapeo;
            return Filas;
        }
    }

    /// <summary>Lo que recibió <see cref="IPruebaDeGeneracion.Probar"/>, con las referencias tal cual llegaron.</summary>
    internal sealed class LlamadaAProbar
    {
        public byte[] Archivo;
        public string Huella;
        public IReadOnlyList<FilaDeMapeo> Mapeo;
        public int Capacidad;
    }

    /// <summary>
    /// <see cref="IPruebaDeGeneracion"/> falso: la huella que calcula se arma UNA vez por instancia (si <c>Probar</c> la recibe, es la que <c>CalcularHuella</c> devolvió); el resultado de <c>Probar</c> se configura.
    /// </summary>
    internal sealed class PruebaDeGeneracionContadora : IPruebaDeGeneracion
    {
        public string Huella { get; } = "huella-falsa-" + Guid.NewGuid().ToString("N");
        public ResultadoDePruebaDeGeneracion Resultado { get; set; } = ResultadoDePruebaDeGeneracion.Exito();
        public Exception FallaAlCalcularLaHuella { get; set; }
        public Exception FallaAlProbar { get; set; }

        public List<byte[]> HuellasCalculadas { get; } = new List<byte[]>();
        public List<LlamadaAProbar> Pruebas { get; } = new List<LlamadaAProbar>();

        public string CalcularHuella(byte[] contenido)
        {
            HuellasCalculadas.Add(contenido);
            if (FallaAlCalcularLaHuella != null) throw FallaAlCalcularLaHuella;
            return Huella;
        }

        public ResultadoDePruebaDeGeneracion Probar(byte[] archivo, string huella, IReadOnlyList<FilaDeMapeo> mapeo, int capacidad)
        {
            Pruebas.Add(new LlamadaAProbar { Archivo = archivo, Huella = huella, Mapeo = mapeo, Capacidad = capacidad });
            if (FallaAlProbar != null) throw FallaAlProbar;
            return Resultado;
        }
    }
}
