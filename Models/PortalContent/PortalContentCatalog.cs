namespace Exatech_Indotel_API.Models.PortalContent;

public record PortalTextDefinition(string Key, string Label, string Group, string DefaultValue, int MaxLength = 500);

public static class PortalContentCatalog
{
    public static readonly IReadOnlyList<PortalTextDefinition> Fields = Array.AsReadOnly(new[]
    {
        new PortalTextDefinition("portal.name", "Nombre del portal", "General", "Portal Indotel", 100),
        new("portal.publicTag", "Texto del encabezado público", "General", "Gestión de beneficiarios", 150),
        new("portal.groupCredit", "Crédito del grupo", "General", "Una empresa del Grupo Exatech", 150),
        new("sidebar.slogan", "Eslogan del menú", "Menú", "Conectamos oportunidades", 150),
        new("sidebar.description", "Descripción del menú", "Menú", "Un espacio para acercar la conectividad a más hogares."),
        new("login.tagline", "Lema", "Inicio de sesión", "CONECTIVIDAD CON PROPOSITO", 150),
        new("login.headline", "Eslogan principal", "Inicio de sesión", "Conectamos hogares.\nAcercamos oportunidades.", 250),
        new("login.description", "Descripción", "Inicio de sesión", "El espacio de trabajo de tu equipo para acompañar a cada beneficiario del programa Indotel."),
        new("login.storyFooter", "Identificación del portal", "Inicio de sesión", "EXATECH · PORTAL INDOTEL", 150),
        new("login.eyebrow", "Texto de bienvenida", "Inicio de sesión", "BIENVENIDO A EXATECH", 150),
        new("login.title", "Título del formulario", "Inicio de sesión", "Qué bueno tenerte aquí.", 200),
        new("login.subtitle", "Descripción del formulario", "Inicio de sesión", "Inicia sesión para continuar con tu trabajo."),
        new("login.note", "Nota de acceso", "Inicio de sesión", "Acceso exclusivo para el equipo autorizado.", 200),
        new("home.eyebrow", "Antetítulo", "Página principal", "TU ESPACIO DE TRABAJO", 150),
        new("home.title", "Título", "Página principal", "Todo empieza con una conexión.", 250),
        new("home.description", "Descripción de bienvenida", "Página principal", "Gestiona beneficiarios y contratos desde un mismo lugar."),
        new("home.heroLabel", "Etiqueta del programa", "Página principal", "PROGRAMA INDOTEL", 150),
        new("home.headline", "Eslogan principal", "Página principal", "Más cerca de las personas.\nMás hogares conectados.", 250),
        new("home.heroDescription", "Descripción del programa", "Página principal", "Acompaña a cada beneficiario, desde la primera consulta hasta la gestión de su servicio."),
        new("home.caption", "Lema secundario", "Página principal", "Conectividad que acerca", 150),
        new("home.helpTitle", "Título de la nota informativa", "Página principal", "Un flujo para cada localidad", 150),
        new("home.helpDescription", "Nota informativa", "Página principal", "Selecciona la localidad al consultar. El portal identificará automáticamente la fase correspondiente."),
        new("client.title", "Título", "Creación de clientes", "Un nuevo hogar conectado.", 250),
        new("client.description", "Descripción", "Creación de clientes", "Completa los datos del cliente para continuar con su registro."),
        new("lookup.title", "Título", "Consulta de beneficiarios", "Cada conexión empieza aquí.", 250),
        new("lookup.description", "Descripción", "Consulta de beneficiarios", "Consulta la cédula y verifica si el beneficiario puede continuar con su registro.")
    });
}
