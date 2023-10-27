namespace DecideLab;

public record State();
public record Command();
public record Event();
public record Decider<TCommand, TEvent, TState>(
    State InitialState,
    Func<TCommand, TState, TEvent[]> Decide,
    Func<TState, TEvent, TState> Evolve,
    Func<TState, bool> IsTerminal);

public record Decider(
    State InitialState,
    Func<Command, State, Event[]> Decide,
    Func<State, Event, State> Evolve,
    Func<State, bool> IsTerminal)
    : Decider<Command, Event, State>(InitialState, Decide, Evolve, IsTerminal);

public record GameState(string Name) : State();
public record StartGame(string Name) : Command();
public record GameStarted(string Name) : Event();
public record GameDecider() : Decider(
            new GameState("None"),
            (c, s) => (c, s) switch
            {
                (StartGame cmd, GameState state) => new[] { new GameStarted(cmd.Name) },
                _ => Array.Empty<Event>()
            },
            (s, e) => (s, e) switch
            {
                (GameState state, GameStarted @event) => state with { Name = @event.Name },
                _ => s
            },
            s => false);

public record GameDecider2()
    : Decider<Command, Event, GameState>(
        new GameState("None"),
            (c, s) => c switch
            {
                StartGame cmd => new[] { new GameStarted(cmd.Name) },
                _ => Array.Empty<Event>()
            },
            (s, e) => e switch
            {
                GameStarted @event => s with { Name = @event.Name },
                _ => s
            },
            s => false
        );

public interface IEventStore
{
    (IAsyncEnumerable<Event> Events, long Version) LoadStream(string streamName);
    Task<long> Append(long expectedVersion, params Event[] events);
}

public static class ApplicationService
{
    public static async Task Execute(
        this IEventStore store,
        string streamName,
        Command command,
        Decider decider, 
        Func<Event, Task> pub)
    {
        var (history, version) = store.LoadStream(streamName);
        var currentState = await history.AggregateAsync(decider.InitialState, decider.Evolve);
        var events = decider.Decide(command, currentState);
        await store.Append(version, events);
        foreach (var e in events)
            await pub(e);
    }
}

public static class Lab
{
    public static async Task Main()
    {
        IEventStore store = null;
        var cmd = new StartGame("test");
        await store.Execute("test", cmd, new GameDecider(), e => Task.CompletedTask);
    }
}
