# Trois fonctionnalités à développer

## 1. Session locale par circuit Blazor

Implémenter ITaskBoardService/TaskBoardService : comptes synthétiques
alice@example.test / Alice!234 et bob@example.test / Bob!234. LoginAsync retourne
true seulement pour les identifiants exacts et expose CurrentUser ; tout échec
supprime l'identité précédente. Logout retire l'identité. Un accès List/Add/
SetCompleted/Delete sans identité lève UnauthorizedAccessException. Service scoped,
pas d'identité statique ni de stockage navigateur. Pas de JWT/API pour ce tester.

## 2. Tâches privées et filtre

Mémoire isolée par email dans le service ; retrouver ses tâches après logout/login
durant la même instance de service. AddAsync trim le titre, exige 1..120 caractères,
lève ArgumentException sinon sans mutation, alloue un Guid neuf et Completed=false.
ListAsync All/Active/Completed retourne respectivement toutes/false/true sans
détruire les autres. SetCompletedAsync garde id/title et retourne l'état enregistré.
DeleteAsync supprime uniquement la tâche du compte actuel. ID absent ou possédé par
un autre compte : KeyNotFoundException, sans mutation. Conserver interfaces publiques.

## 3. Composant interactif accessible

TaskBoardPage conserve h1 `Task Board`. Champ Email, champ Password type=password,
bouton Sign in. Après login : liste, champ Task title, Add task, checkbox et Delete
par tâche, select Filter valeurs `All`, `Active`, `Completed`, bouton Sign out.
Le composant fonctionne aussi si le service est déjà connecté avant son rendu.
Les erreurs ont role=alert. Login/logout mettent à jour la vue ; logout retire
toute donnée privée de la vue. Opérations réussies rafraîchissent la liste filtrée.
Garder les hooks de test suivants (attribut data-testid) :

| Hook | Élément |
|---|---|
| email / password / login | Champs et bouton de connexion |
| task-title / add | Création |
| tasks | Conteneur liste, présent même si vide lorsque connecté |
| task-row / complete / delete | Ligne, checkbox, bouton par tâche |
| filter / logout | Select et bouton |

Pas de CSS/framework imposé, DB, service externe, inscription ou déploiement.
