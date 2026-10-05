<p align="center"><img src="docs/images/banner.png" alt="Uber Eats Overlay" width="100%"></p>

# Uber Eats Overlay

Petit overlay Windows (transparent, déplaçable, toujours au-dessus) qui affiche le suivi de ta livraison Uber Eats et se met à jour tout seul. / A small always-on-top, transparent, draggable Windows overlay that tracks your Uber Eats delivery and refreshes automatically.

WPF + WebView2, .NET 9. ~60 Mo de RAM au repos / ~60 MB RAM at idle.

## Fonctionnalités / Features

- Carte translucide déplaçable, position mémorisée / draggable translucent card, position remembered
- Réglage de la transparence (le texte reste net) / transparency slider (text stays sharp)
- Mode clic traversant (`Ctrl+Alt+T`), afficher/masquer (`Ctrl+Alt+U`) / click-through and show/hide hotkeys
- Visible uniquement pendant une commande en cours / only visible while an order is in progress
- Temps restant + heure d'arrivée, notifications Windows / time remaining + ETA, Windows notifications
- Français, English, Español (auto selon Windows) ; heure 24 h / 12 h
- Démarrage avec Windows (option) / start with Windows (option)

## Aperçu / Preview

Illustrations avec des données fictives. / Illustrations with sample data.

![États / States](docs/images/overlay-states.png)
![Transparence et clic traversant / Transparency and click-through](docs/images/overlay-transparent.png)

## Installation

Télécharge `UberOverlay-Setup.exe` depuis les [Releases](../../releases) : installateur autonome, sans droits admin, sans .NET requis.
Download `UberOverlay-Setup.exe` from Releases: standalone, no admin rights, no .NET needed.

Au premier lancement, clique sur le bouton profil et connecte-toi à Uber Eats. / On first launch, click the profile button and sign in to Uber Eats.

## Fonctionnement / How it works

Uber Eats n'a pas d'API publique : l'app ouvre brièvement un navigateur WebView2 (avec ta session) toutes les 30–60 s, lit la page de suivi, puis le ferme pour économiser la RAM. Ton mot de passe n'est jamais lu ni stocké par l'app ; la session reste dans `%AppData%\UberOverlay`.

Uber has no public API: the app briefly opens a WebView2 browser (with your session) every 30–60 s, reads the tracking page, then closes it to save RAM. Your password is never read or stored by the app.

> ⚠️ Projet non officiel, non affilié à Uber. La lecture dépend de la structure de la page Uber Eats et peut casser. Unofficial, not affiliated with Uber; it depends on Uber's page layout and may break.

Pour déboguer la lecture : lancer avec la variable `UBER_DEBUG=1` (journal dans `%AppData%\UberOverlay\debug.log`). Ce journal contient le texte de ta page de commandes : ne le partage pas tel quel.

## Compiler / Build

Prérequis : SDK .NET 9.

```powershell
dotnet run --project UberOverlay.Net -c Release     # lancer
powershell -File installer\build.ps1                 # générer installer\out\UberOverlay-Setup.exe
```
