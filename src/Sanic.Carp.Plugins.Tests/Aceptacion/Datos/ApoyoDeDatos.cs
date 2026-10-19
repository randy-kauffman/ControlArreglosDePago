using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Aceptacion;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Datos
{
    /// <summary>
    /// Apoyos de las pruebas de aceptación del lote 4 (Datos e Infraestructura). Viven en <c>Aceptacion/</c> para que el constructor no pueda tocarlos.
    /// </summary>
    internal static class ApoyoDeDatos
    {
        public const string Conversacion = "sanic_carp_tbl_conversacion";
        public const string Arreglo = "sanic_carp_tbl_arreglo";
        public const string Cuota = "sanic_carp_tbl_cuota";
        public const string Cliente = "sanic_carp_tbl_cliente";
        public const string VersionDePlantilla = "sanic_carp_tbl_versionplantilla";
        public const string Mapeo = "sanic_carp_tbl_mapeocelda";
        public const string Generacion = "sanic_carp_tbl_generacionformato";
        public const string Correo = "email";
        public const string Usuario = "systemuser";
        public const string Team = "team";

        public static readonly Guid UsuarioA = new Guid("a1a1a1a1-0000-4000-8000-000000000001");
        public static readonly Guid UsuarioB = new Guid("a1a1a1a1-0000-4000-8000-000000000002");

        public static System.ServiceModel.FaultException<OrganizationServiceFault> Falla(Action accion)
        {
            return Dobles.ApoyoDeDobles.Falla(accion);
        }

        /// <summary>Misma verificación de forma que las pruebas del doble (código, sin InnerException, niveles de InnerFault medidos).</summary>
        public static void AssertFalla(System.ServiceModel.FaultException<OrganizationServiceFault> falla, int codigo, string fragmentoDelMensaje = null)
        {
            Dobles.ApoyoMedido.AssertFalla(falla, codigo, fragmentoDelMensaje);
        }

        public static IReadOnlyCollection<string> Cols(params string[] columnas)
        {
            return columnas;
        }

        /// <summary>Siembra la fila armada y devuelve su id.</summary>
        public static Guid Sembrar(OrganizationServiceEnMemoria almacen, ConstructorDeFila fila)
        {
            almacen.Sembrar(fila.Construir());
            return fila.Id;
        }

        public static Guid SembrarTeam(OrganizationServiceEnMemoria almacen)
        {
            return Sembrar(almacen, ConstructorDeDatos.UnTeam("ZZF Team " + Guid.NewGuid().ToString("N"), Guid.NewGuid(), false));
        }

        public static Guid SembrarUsuario(OrganizationServiceEnMemoria almacen, Guid? id = null)
        {
            return Sembrar(almacen, ConstructorDeDatos.UnUsuario(id ?? Guid.NewGuid()));
        }

        public static Guid SembrarCliente(OrganizationServiceEnMemoria almacen)
        {
            return Sembrar(almacen, ConstructorDeDatos.UnCliente());
        }

        public static Guid SembrarConversacion(OrganizationServiceEnMemoria almacen)
        {
            return Sembrar(almacen, ConstructorDeDatos.UnaConversacion());
        }

        public static EntityReference Ref(string tabla, Guid id)
        {
            return new EntityReference(tabla, id);
        }

        public static IReadOnlyList<ConsultaRegistrada> ConsultasDe(OrganizationServiceEnMemoria almacen, string tabla)
        {
            return almacen.Consultas.Where(c => string.Equals(c.Tabla, tabla, StringComparison.Ordinal)).ToList();
        }

        /// <summary>La consulta pide EXACTAMENTE esas columnas (como conjunto, ordinal) y nunca <c>ColumnSet(true)</c>.</summary>
        public static void AssertColumnas(ConsultaRegistrada consulta, params string[] esperadas)
        {
            Assert.False(consulta.TodasLasColumnas, "ColumnSet(true) en una consulta de " + consulta.Tabla);
            Assert.Equal(esperadas.OrderBy(x => x, StringComparer.Ordinal).ToList(), consulta.Columnas.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList());
        }

        public static void AssertEscribio(EscrituraRegistrada escritura, OperacionDeDatos operacion, string tabla, params string[] columnas)
        {
            Assert.Equal(operacion, escritura.Operacion);
            Assert.Equal(tabla, escritura.Tabla);
            Assert.Equal(columnas.OrderBy(x => x, StringComparer.Ordinal).ToList(), escritura.Columnas.OrderBy(x => x, StringComparer.Ordinal).ToList());
        }

        public static void AssertSinColumnaDeId(FilaDeDatos fila, string tabla)
        {
            var id = string.Equals(tabla, Correo, StringComparison.Ordinal) ? "activityid" : tabla + "id";
            Assert.False(fila.Valores.ContainsKey(id), "Valores no debe traer el atributo del id primario '" + id + "'.");
        }

        // ---- Lectura del código fuente para las guardias ----

        /// <summary>
        /// Quita los comentarios (cada uno se reemplaza por un espacio) respetando los literales: <c>"..."</c> con escapes, <c>@"..."</c> con <c>""</c>, <c>$"..."</c> y <c>$@"..."</c>
        /// (sin anidar: se lee como un texto común) y <c>'x'</c>. Un <c>//</c> o un <c>/*</c> dentro de un literal es texto, no un comentario.
        /// </summary>
        internal static string SinComentarios(string texto)
        {
            var salida = new System.Text.StringBuilder(texto.Length);
            int i = 0;
            int n = texto.Length;
            while (i < n)
            {
                char c = texto[i];
                if (c == '/' && i + 1 < n && texto[i + 1] == '/')
                {
                    while (i < n && texto[i] != '\r' && texto[i] != '\n') i++;
                    salida.Append(' ');
                }
                else if (c == '/' && i + 1 < n && texto[i + 1] == '*')
                {
                    i += 2;
                    while (i < n && !(texto[i] == '*' && i + 1 < n && texto[i + 1] == '/')) i++;
                    i = Math.Min(i + 2, n);
                    salida.Append(' ');
                }
                else if (c == '"')
                {
                    // Prefijo ya emitido: @" , $" , $@" o @$" . Solo @ hace el texto verbatim.
                    bool verbatim = false;
                    for (int k = salida.Length - 1; k >= 0 && salida.Length - k <= 2; k--)
                    {
                        if (salida[k] == '@') { verbatim = true; break; }
                        if (salida[k] != '$') break;
                    }
                    salida.Append(c);
                    i++;
                    while (i < n)
                    {
                        char d = texto[i];
                        if (verbatim)
                        {
                            if (d == '"' && i + 1 < n && texto[i + 1] == '"') { salida.Append("\"\""); i += 2; continue; }
                            if (d == '"') break;
                        }
                        else
                        {
                            if (d == '\\' && i + 1 < n) { salida.Append(d).Append(texto[i + 1]); i += 2; continue; }
                            if (d == '"') break;
                        }
                        salida.Append(d);
                        i++;
                    }
                    if (i < n) { salida.Append('"'); i++; }
                }
                else if (c == '\'')
                {
                    salida.Append(c);
                    i++;
                    while (i < n && texto[i] != '\r' && texto[i] != '\n')
                    {
                        char d = texto[i];
                        if (d == '\\' && i + 1 < n) { salida.Append(d).Append(texto[i + 1]); i += 2; continue; }
                        salida.Append(d);
                        i++;
                        if (d == '\'') break;
                    }
                }
                else
                {
                    salida.Append(c);
                    i++;
                }
            }
            return salida.ToString();
        }

        /// <summary>Los <c>.cs</c> de una carpeta del ensamblado (por ejemplo <c>Datos</c>), sin <c>bin</c> ni <c>obj</c> y SIN comentarios. Falla si la carpeta no existe o está vacía.</summary>
        public static IReadOnlyList<KeyValuePair<string, string>> FuentesDe(string carpetaRelativa)
        {
            var directorio = new DirectoryInfo(AppContext.BaseDirectory);
            while (directorio != null && !Directory.Exists(Path.Combine(directorio.FullName, "src", "Sanic.Carp.Plugins"))) directorio = directorio.Parent;
            if (directorio == null) throw new DirectoryNotFoundException("No se encontró 'src/Sanic.Carp.Plugins' subiendo desde '" + AppContext.BaseDirectory + "'.");
            var raiz = Path.Combine(directorio.FullName, "src", "Sanic.Carp.Plugins");
            var inicio = carpetaRelativa.Length == 0 ? raiz : Path.Combine(raiz, carpetaRelativa);
            Assert.True(Directory.Exists(inicio), "No existe la carpeta de fuentes '" + carpetaRelativa + "'.");
            var resultado = new List<KeyValuePair<string, string>>();
            foreach (var archivo in Directory.EnumerateFiles(inicio, "*.cs", SearchOption.AllDirectories).OrderBy(a => a, StringComparer.Ordinal))
            {
                var relativa = Path.GetRelativePath(raiz, archivo).Replace(Path.DirectorySeparatorChar, '/');
                if (relativa.StartsWith("bin/", StringComparison.Ordinal) || relativa.StartsWith("obj/", StringComparison.Ordinal)) continue;
                var texto = File.ReadAllText(archivo, System.Text.Encoding.UTF8);
                texto = SinComentarios(texto);
                resultado.Add(new KeyValuePair<string, string>(relativa, texto));
            }
            Assert.NotEmpty(resultado);
            return resultado;
        }
    }
}
