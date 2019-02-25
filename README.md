# Multi-Threaded Entity Framework Core Samples

Entity Framework DB context's [are not thread safe](https://docs.microsoft.com/ef/core/querying/async). If an ASP.NET Core app 
wishes to process requests in a multi-threaded way while using Entity 
Framework Core, it's important to carefully manage DbContexts so that a 
single context isn't used on multiple threads simultaneously.

This simple sample app demonstrates three techniques for safely using 
DbContexts in multi-threaded environments.

## About the sample

The sample uses Entity Framework Core to add and remove 'book' entities 
to to and from a local SQL database. Because many books may be added/removed 
at once, the work is done in parallel by a configurable number of tasks (16, 
by default).

## Multi-threaded EF Core approaches

The first API in `BooksController` (`ModifyBooks`) will, by design, fail 
because it incorrectly uses a `DbContext` on multiple threads simultaneously. 

The three other action methods in the controller demonstrate three options 
for fixing such a problem. Which option is best will depend on the 
architecture of the app being worked on. All of the suggested fixes have some 
necessary drawbacks that may make them less suitable for some environments, 
though I have tried to list the options from most-to-least likely to be 
useful.

### Fix 1: Use a transient DbContext
The first option (demonstrated in `ModifyBooksFix1`) is often the simplest. 
By making the DbContext's lifetime transient (in Startup.cs), individual 
parallel workers can create their own instances of the DbContext by simply 
retrieving it from the app's `IServiceProvider`.

**Drawbacks:** One potential challenge with this solution is that it means
DbContexts used by an API's dependencies may not be the same instance as 
used by that API unless the context is explicitly passed as a parameter 
(since every instance of the context retrieved from DI will be unique).

### Fix 2: Create DbContexts, as needed, from DI-delivered DbContextOptions
A second option (which is similar to the last one) is to create DbContexts
directly (using the DbContext constructor) instead of retrieving them from
dependency injection. This is possible because the `DbContextOptions` needed 
to create a DbContext can be in dependency injection and will cause no 
threading issues if reused in parallel.

Depending on the needs of the project, other (non-parallel) code paths can 
still retrieve DbContexts from DI or create them from options, as necessary.

**Drawbacks:** If DbContexts are sometimes retrieveed from DI and sometimes 
created from options, there could be issues with dependencies sometimes 
sharing a context with a caller and other times not. If the DbContexts are 
always shared, this becomes similar to fix #1 except with explicit DbContext 
creation (based on DI-retrieved options).

### Fix 3: Use nested service provider scopes
If it is desirable for DbContexts to continue having scoped lifetimes, it is 
still possible to retrieve unique DbContexts for different parallel workers 
by creating new service scopes for each of the workers. This will maintain 
scoped behavior for the DbContext (so that dependencies retrieving from 
the DI container will get the same isntance, for example).

**Drawbacks:** The drawback of this approach is that creating a new scope for 
the parallel workers means that all scoped services resolved by them or their 
dependencies will be distinct from those used by other workers or from the 
workers' calling method. Depending on the project's behavior this may by
undesirable.

## Running the sample

To run the sample, simply launch the app (via `dotnet run` or f5 from VS) and 
use the Swagger UI to run either the broken API or any of the three fixed 
APIs by providing a count for the number of items to add and remove (the 
default value of 100 tends to be a good value in my experience).

The three fixed APIs should return 200. The broken API will fail with exceptions similar to this:

```
An exception occurred in the database while saving changes for context type 'SampleWebApi.Data.BookContext'.
    System.InvalidOperationException: A second operation started on this context before a previous operation completed. Any instance members are not guaranteed to be thread safe.
        at Microsoft.EntityFrameworkCore.Internal.ConcurrencyDetector.EnterCriticalSection()
        at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.StateManager.SaveChangesAsync(IReadOnlyList`1 entriesToSave, CancellationToken cancellationToken)
        at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.StateManager.SaveChangesAsync(Boolean acceptAllChangesOnSuccess, CancellationToken cancellationToken)
        at Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(Boolean acceptAllChangesOnSuccess, CancellationToken cancellationToken)
```