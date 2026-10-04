# Contrat des trois fonctionnalités

## 1. Authentification JWT

Implémenter `POST /api/auth/login` avec JSON `{ "email": "...", "password": "..." }`.
Comptes synthétiques fixes : `alice@example.test` / `Alice!234`, `bob@example.test` /
`Bob!234`, `admin@example.test` / `Admin!234`. Rôles respectifs `User`, `User`, `Admin`.
Ils ne représentent aucun compte réel. Email/password vides ou blancs : 400 ;
identifiants inconnus ou incorrects : 401 ; succès : 200 et `{ "accessToken": "JWT" }`.
JWT HS256, `sub` = email, rôle signé, échéance 15 minutes, issuer/audience/signing key
lus dans `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` (configuration .NET).
Aucune clé de production dans les sources. Validation complète signature, issuer,
audience et expiration ; aucune tolérance à un token expiré de dix minutes.
Les tests fournissent une clé synthétique par configuration. Pour un essai manuel,
définir `Jwt__SigningKey` (64 caractères minimum), `Jwt__Issuer=benchmark-task-api`,
`Jwt__Audience=benchmark-task-client` dans l'environnement du processus.

## 2. Tâches privées

Toutes les routes ci-dessous exigent un bearer JWT valide ; sinon 401 avant toute mutation.
Stockage mémoire par instance de serveur, initialement vide, thread-safe, séparé par `sub`.
Le propriétaire est toujours dérivé du JWT. DTO public : `{ "id": "GUID", "title": "texte", "completed": false }`.

| Route | Contrat |
|---|---|
| `GET /api/tasks` | 200, tableau des seules tâches de l'auteur ; tableau vide autorisé |
| `GET /api/tasks?completed=true` ou `false` | Filtre exact sur completed ; absent = toutes |
| `POST /api/tasks` | `{ "title": "...", "completed": false }` ; 201, DTO et Location `/api/tasks/{id}` |
| `PUT /api/tasks/{id}` | Même entrée, remplace title/completed ; 200 et DTO |
| `DELETE /api/tasks/{id}` | 204 sans contenu |

Title est trimé puis contient 1 à 120 caractères inclus, sinon 400 sans mutation.
ID neuf non vide et unique à la création. ID absent ou appartenant à un autre compte :
404 sur PUT/DELETE, sans divulgation ni modification. Un admin ne voit pas davantage
dans `/api/tasks`. Pas de DB, inscription, refresh token, déploiement ni service externe.
Préserver `GET /health` public (200, `{ "status": "ok" }`) et les routes inconnues (404).

## 3. Interface React

Développer `frontend/src/App.tsx` : écran de connexion avec labels `Email`,
`Password`, bouton `Sign in`. Appeler `/api/auth/login`, conserver le token seulement
en mémoire, utiliser `Authorization: Bearer <token>` pour toutes les routes tâches.
Afficher erreurs dans un élément `role="alert"`. Ne pas faire de login local factice.
Après connexion : liste des titres, champ `Task title`, bouton `Add task`, select
`Filter` avec valeurs `all`, `active`, `completed`, checkbox par ligne avec
label `Complete <title>`, bouton par ligne `Delete <title>`, bouton `Sign out`.
Créer par POST, compléter par PUT avec title/completed, supprimer par DELETE.
Filtrer sans perdre les tâches originales ; `all` les réaffiche. Ne modifier l'écran
que si l'opération réussit. Une réponse 401 sur une route tâches retire le token et
les tâches et revient à la connexion. Logout fait de même ; aucun stockage local
ou cookie requis. Préserver le titre public `Task Board`. App.tsx peut importer
d'autres modules sous `frontend/src`. Le serveur Vite proxifie `/api` vers 5080.
