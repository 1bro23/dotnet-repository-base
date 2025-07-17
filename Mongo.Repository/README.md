# Mongo.Repository

A generic and extensible MongoDB repository base for .NET applications.  
Supports filtering, sorting, pagination, and real-time streaming via `IAsyncEnumerable`.

## Features

- `MongoRepositoryBase<Model, Filter, Update>` abstraction
- Filtered querying with projection
- Pagination and sorting
- Streaming results with `IAsyncEnumerable`
- Built-in support for MongoDB.Driver

## Installation

Install via NuGet: