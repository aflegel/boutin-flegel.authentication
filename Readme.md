# BoutinFlegel Authentication

## Goals and Overview
The project is intended for consumption by API services and not directly by a UI.
The intended protocol is [gRPC](https://grpc.io/).

## Roadmap

### Stage 1
* Get the authentication server running on an in memory database. The default is `SqlLite`.
* Configure a token auth flow

### Stage 2
* Configure a proper database solution
* Configure an api to connect and test authentication

### Stage 3
* Configure the Google external auth
* Expand authenticated apps region