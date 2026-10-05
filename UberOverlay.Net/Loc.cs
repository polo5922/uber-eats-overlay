using System.Globalization;

namespace UberOverlay;

/// <summary>Textes de l'app en français, anglais et espagnol. Langue "auto" = langue de Windows.</summary>
public static class Loc
{
    public static string Lang { get; private set; } = "en";

    public static readonly (string Code, string Name)[] Languages =
        { ("fr", "Français"), ("en", "English"), ("es", "Español") };

    /// <summary>Convertit le réglage ("auto", "fr", "en", "es") en langue effective.</summary>
    public static void Apply(string setting)
    {
        var code = setting;
        if (code is not ("fr" or "en" or "es"))
            code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        Lang = code is "fr" or "es" ? code : "en";
    }

    public static string Get(string key) => T.TryGetValue(key, out var v) ? (Lang == "fr" ? v.fr : Lang == "es" ? v.es : v.en) : key;

    static readonly Dictionary<string, (string fr, string en, string es)> T = new()
    {
        ["loading"] = ("Chargement…", "Loading…", "Cargando…"),
        ["loggedOut"] = ("Non connecté", "Not signed in", "No conectado"),
        ["loggedOutSub"] = ("Clique sur le bouton profil pour te connecter", "Click the profile button to sign in", "Pulsa el botón de perfil para iniciar sesión"),
        ["idle"] = ("Rien en route 🍽️", "Nothing on the way 🍽️", "Nada en camino 🍽️"),
        ["idleSub"] = ("Aucune commande en cours, un petit creux ?", "No order in progress, feeling hungry?", "Ningún pedido en curso, ¿te apetece algo?"),
        ["delivered"] = ("Commande livrée ✓", "Order delivered ✓", "Pedido entregado ✓"),
        ["error"] = ("Erreur", "Error", "Error"),
        ["orderDefault"] = ("Commande en cours", "Order in progress", "Pedido en curso"),
        ["imminent"] = ("Arrivée imminente", "Arriving any moment", "Llegada inminente"),
        ["minLeft"] = ("{0} min restantes", "{0} min left", "Quedan {0} min"),
        ["hmLeft"] = ("{0} h {1:00} restantes", "{0} h {1:00} left", "Quedan {0} h {1:00}"),
        ["arrival"] = ("Arrivée à {0}", "Arriving at {0}", "Llegada a las {0}"),
        ["updated"] = ("MAJ", "Upd.", "Act."),
        ["balloonDelivered"] = ("Commande livrée !", "Order delivered!", "¡Pedido entregado!"),
        ["balloonUpdated"] = ("Commande mise à jour", "Order updated", "Pedido actualizado"),
        ["tipRefresh"] = ("Actualiser", "Refresh", "Actualizar"),
        ["tipLogin"] = ("Se connecter", "Sign in", "Iniciar sesión"),
        ["tipSwitch"] = ("Changer de compte Uber Eats", "Switch Uber Eats account", "Cambiar de cuenta de Uber Eats"),
        ["mSwitch"] = ("Changer de compte Uber Eats", "Switch Uber Eats account", "Cambiar de cuenta de Uber Eats"),
        ["tipGhost"] = ("Clic traversant (Ctrl+Alt+T pour annuler)", "Click-through (Ctrl+Alt+T to turn off)", "Clic a través (Ctrl+Alt+T para desactivar)"),
        ["tipOpacity"] = ("Transparence", "Transparency", "Transparencia"),
        ["tipQuit"] = ("Quitter", "Quit", "Salir"),
        ["mToggle"] = ("Afficher / masquer (Ctrl+Alt+U)", "Show / hide (Ctrl+Alt+U)", "Mostrar / ocultar (Ctrl+Alt+U)"),
        ["mLogin"] = ("Se connecter à Uber Eats", "Sign in to Uber Eats", "Iniciar sesión en Uber Eats"),
        ["mRefresh"] = ("Actualiser", "Refresh", "Actualizar"),
        ["mGhost"] = ("Mode clic traversant (Ctrl+Alt+T)", "Click-through mode (Ctrl+Alt+T)", "Modo clic a través (Ctrl+Alt+T)"),
        ["mStartup"] = ("Lancer au démarrage de Windows", "Start with Windows", "Iniciar con Windows"),
        ["mLanguage"] = ("Langue", "Language", "Idioma"),
        ["mTimeFormat"] = ("Format de l'heure", "Time format", "Formato de hora"),
        ["mAuto"] = ("Automatique", "Automatic", "Automático"),
        ["m24"] = ("24 heures", "24-hour", "24 horas"),
        ["m12"] = ("12 heures (AM/PM)", "12-hour (AM/PM)", "12 horas (AM/PM)"),
        ["mQuit"] = ("Quitter", "Quit", "Salir"),
        ["loginTitle"] = ("Connexion Uber Eats", "Uber Eats sign-in", "Inicio de sesión en Uber Eats"),
    };
}
