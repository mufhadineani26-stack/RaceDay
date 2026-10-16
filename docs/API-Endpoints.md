# RaceDay API – Implemented Endpoints (Part 2)

All routes start with `/api/`. Authentication is session based: `POST /api/auth/login` stores the user's id and role in a server-side session and returns an HttpOnly `.RaceDay.Session` cookie that the client sends on every following request.

**Role Required** values: **None** (public), **Any** (logged in, either role), **Organiser**, **Participant**.
Every protected route returns **401 Unauthorized** when not logged in and **403 Forbidden** when the logged-in role is not allowed. These are not repeated in every row below.

## Authentication

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | `/api/auth/register` | Creates an account; the user picks Organiser or Participant. Password is hashed before saving. | None | `{ firstName, lastName, email, password, phoneNumber, dateOfBirth, role }` | 201 Created – user summary · 400 Bad Request – validation / invalid role · 409 Conflict – email already registered |
| POST | `/api/auth/login` | Verifies credentials and starts a session holding the user's id and role. | None | `{ email, password }` | 200 OK – `{ userId, fullName, email, role }` · 401 Unauthorized – invalid email or password |
| POST | `/api/auth/logout` | Clears the current session. | Any | None | 200 OK – confirmation |
| GET | `/api/auth/me` | Returns who is logged in for this session. | Any | None | 200 OK – `{ userId, fullName, email, role }` |

## User Profile

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/profile` | Returns the logged-in user's own profile. | Any | None | 200 OK – profile · 404 Not Found – account removed |
| PUT | `/api/profile` | Updates own name, phone number and date of birth (email and role are fixed). | Any | `{ firstName, lastName, phoneNumber, dateOfBirth }` | 200 OK – updated profile · 400 Bad Request – validation |
| PUT | `/api/profile/password` | Changes own password after checking the current one. | Any | `{ currentPassword, newPassword }` | 200 OK – confirmation · 400 Bad Request – current password wrong |

## Events

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/event-types` | Lists event types (Run, Walk, Cycle). | Any | None | 200 OK – list of types |
| GET | `/api/events?type=&upcomingOnly=&search=` | Lists events, soonest first, with optional filters. | Any | None | 200 OK – list of events |
| GET | `/api/events/{id}` | Returns one event. | Any | None | 200 OK – event · 404 Not Found |
| GET | `/api/events/mine` | Lists events created by the logged-in Organiser. | Organiser | None | 200 OK – list of events |
| POST | `/api/events` | Creates an event owned by the logged-in Organiser. | Organiser | `{ name, description, eventDate, location, distanceKm, eventType }` | 201 Created – event · 400 Bad Request – validation, unknown type or past date |
| PUT | `/api/events/{id}` | Updates an event the Organiser created. | Organiser (owner) | `{ name, description, eventDate, location, distanceKm, eventType }` | 200 OK – event · 400 Bad Request · 403 Forbidden – not owner · 404 Not Found |
| DELETE | `/api/events/{id}` | Deletes an event and its categories. | Organiser (owner) | None | 204 No Content · 403 Forbidden – not owner · 404 Not Found · 409 Conflict – event has enrolments |

## Categories

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/events/{eventId}/categories` | Lists the age/distance categories for an event. | Any | None | 200 OK – list · 404 Not Found – event |
| GET | `/api/categories/{id}` | Returns one category. | Any | None | 200 OK – category · 404 Not Found |
| POST | `/api/events/{eventId}/categories` | Adds a category (e.g. Under 20, Senior, 10km, 21km) to an event. | Organiser (owner) | `{ name, description, minAge, maxAge, distanceKm, entryFee, maxParticipants }` | 201 Created – category · 400 Bad Request – e.g. minAge > maxAge · 403 Forbidden · 404 Not Found · 409 Conflict – duplicate name |
| PUT | `/api/categories/{id}` | Updates a category. | Organiser (owner) | same as POST | 200 OK · 400 · 403 · 404 · 409 |
| DELETE | `/api/categories/{id}` | Deletes a category with no enrolments. | Organiser (owner) | None | 204 No Content · 403 · 404 · 409 Conflict – participants enrolled |

## Event Enrolments

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | `/api/events/{eventId}/enrolments` | Enters the logged-in Participant into an event in the chosen category. Records participant + event + category. | Participant | `{ categoryId }` | 201 Created – enrolment · 400 Bad Request – event already happened, category not in event, age limit not met · 404 Not Found – event · 409 Conflict – already entered or category full |
| GET | `/api/enrolments/mine` | Lists the logged-in Participant's enrolments. | Participant | None | 200 OK – list |
| GET | `/api/enrolments/{id}` | Returns one enrolment (owner participant or the event's organiser). | Any | None | 200 OK · 403 Forbidden · 404 Not Found |
| DELETE | `/api/enrolments/{id}` | Withdraws own entry from an upcoming event. | Participant (owner) | None | 204 No Content · 400 Bad Request – event already happened · 403 · 404 |
| GET | `/api/events/{eventId}/enrolments?categoryId=` | Lists all entries for an event the Organiser created. | Organiser (owner) | None | 200 OK – list · 403 Forbidden · 404 Not Found |

## Results

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | `/api/enrolments/{enrolmentId}/result` | Captures finish time and position for a participant after the event; marks the enrolment Completed. | Organiser (owner) | `{ finishTime: "hh:mm:ss", position }` | 201 Created – result · 400 Bad Request – event not yet held / invalid time · 403 · 404 · 409 Conflict – result exists or position taken in category |
| PUT | `/api/results/{id}` | Corrects a captured result. | Organiser (owner) | `{ finishTime, position }` | 200 OK · 400 · 403 · 404 · 409 |
| DELETE | `/api/results/{id}` | Removes a result captured in error. | Organiser (owner) | None | 204 No Content · 403 · 404 |
| GET | `/api/results/mine` | Lists the logged-in Participant's own results (performance history). | Participant | None | 200 OK – list |
| GET | `/api/results/{id}` | Returns one result (owner participant or the event's organiser). | Any | None | 200 OK · 403 · 404 |
| GET | `/api/events/{eventId}/results` | Results board for an event, by category and position. | Any | None | 200 OK – list · 404 Not Found |
