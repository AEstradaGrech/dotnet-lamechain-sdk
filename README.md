[![NuGet](https://img.shields.io/nuget/v/Estrada.OllamaSharp.LameChain.SDK.svg)](https://www.nuget.org/packages/Estrada.OllamaSharp.LameChain.SDK)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0%2B-blue.svg)](https://dotnet.microsoft.com/)

# dotnet-lamechain-sdk
A fluent .NET framework for chaining structured OllamaSharp prompts with sequence and parallel workflows.

## What is LameChain?

LameChain is a lightweight SDK designed to make chaining LLM instructions easy, while enforcing structured output and clear prompt orchestration.

It provides a fluent experience for building command chains that can:

- execute steps in sequence or in parallel
- pass outputs between commands
- boost prompts with additional contextual sources
- generate a final result from multiple intermediate outputs

### The core concepts

LameChain is built around three main components:

1. **Commands**: define how to build prompts, call Ollama, and parse JSON results into typed C# objects.
2. **Chain Steps**: configure and execute command chains, while managing feeds and context.
3. **Fluent Extensions**: expose a readable API for composing valid chains without manual wiring.

This structure makes it easy to use commands standalone, while also enabling richer workflows when you need multiple LLM requests or mixed result types.

## What’s in the box

- A commands-based system for OllamaSharp with structured output support
- Fluent chain extensions for building readable sequence/parallel flows
- Chain rule classes that enforce valid SDK usage
- Inference and Embeddings services with startup configuration helpers

## How it works

### LameChain Commands

A command is the core unit that:

- creates the system prompt
- sends the request to Ollama
- validates output structure
- deserializes JSON into a typed `TResult`

A command is composed of three generic pieces:

- `TCommand`: command implementation and prompt logic
- `TRequest`: input data for the command
- `TResult`: expected structured response type


`TResult` can be any basic type (integers, string, Lists...) or plain C# classes that can be annotated
with JsonProperties (recommended, at least the JsonPropertyName) and `Ollama Lame Attributes` (these are 
Custom Attributes that will help the LLM to understand and generate the expected output).

Example commands range from simple chat prompts:

```csharp
_commandsFactory.GetCommand<ChatPromptCommand, PromptCommandRequest, ChatMessage>()
```

...to rich business commands like:

```csharp
_commandsFactory.GetCommand<CustomerBriefingCommand, CustomerBriefingRequest, CustomerBriefing>()
```

A command builds a system message from three possible sources:

- `_systemMessage`: optionally provided at construction
- `Core Message`: built from default instruction or DB-retrieved instruction
- `Guidance Message`: passed with `TRequest`

These message parts can be combined in different ways:

- use only `_systemMessage`
- use only `Core Message`
- use only `Guidance Message`
- append/prepend guidance to core instruction
- inject data between sections

This flexible composition model helps you adapt prompts to the needs of your application and the strengths of your model.

### Command types

#### Commands without DB access

Also called `DefaultCommand` or `GuidedPromptCommand`.

These are the simplest commands and often work well with only request guidance like "You are a helpful assistant." Example:

```csharp
var command = _factoryCommands.GetCommand<MessagePromptCommand, ChatMessage>(guidance: null, settings: null);
var request = new PromptCommandRequest {
    Prompt = reqDto.UserPrompt,
    GuidanceMessage = reqDto.SystemMessage
};
var chatMessage = await command.Prompt(request);
```

#### Commands with DB access

If you want to reuse rich system instructions from storage, use `DbCommand`.

A `DbCommand` accepts a retriever function such as:

```csharp
Func<string, string, Task<string>>
```

The function can read instructions from a database, document store, or any custom source.

This command type supports:

- `source`
- `messageName`

and may also support a fallback `DefaultMode` when no DB instruction is available.

#### Vector Search Commands

Vector Search command are commands that are supposed to be used in any type of task that requires an Embeddings Generator, so this are mostly commands that will
perform a similarity search on a vectors database to do some task later with an LLM. Because of that, Vector Search Commands have a constructor for tasks without LLM dependency
that requires only an embeddings generator and a query lambda function, and other constructor expecting an inference service in case you want to do some process with the LLM
before or after the similarity search.

#### Sourceable Commands

Sourceable Commands are commands that return ALWAYS a collection of Texts, usually to use them as source for other processes in the chain. The sources
can be gathered in any way (it might be a web search command, a similarity search or LLM-generated content) so use Sourceable Commands to generalize the output
despite the source / gathering process.

(Note: the main purpose of these kind of commands is to create StashedSteps with reusable sources of data)

#### Storeable Commands

If the purpose of a Sourceable Command is to retrieve data from a source and make it available to the chain, the purpose of Storeable Commands is just the opposite:
store the output of a step in a external source. 

This are generic commands that store a lambda function with this signature: Task<TStored> mthdName(TStored item, string collection) (right now the sdk is designed for documental dbs like mongo or Estrada.ChromaDb.Repos)

A Storeable Command must always return the result of the insert, which should be the stored object with its database id, so it can be passed as context to the next step.

#### Core Commands

The framework includes ready-to-use core commands for common use cases, including simple chat, RAG-style prompts, and structured output generation.

##### Atomic Value Commands

Atomic value commands perform small structured requests for values like:

- integers
- booleans
- dates

They are useful when you need the model to compute discrete values that are then used programmatically in your process.

Example:

```csharp
public Task<PurchaseReceipt> GetCustomerDiscount(float appliedDiscount, DateTime daysToNextGift, bool withPurchaseGift);

var receipt = await GetCustomerDiscount(
    await CalculateDiscountCommand("Review purchase history and customer account creation date to calculate...", customerData),
    await CalculateDaysToNextGiftCommand("Based on the data output the minimum date for the next purchase gift etc", customerData),
    await BoolCommand("Based on the provided data, decide if the customer should receive a gift with the purchase", customerData)
);

var customerMessage = await _factory.GetCustomerAnswerCommand(receipt);
```

Atomic commands also support DB-based instructions for models that need stronger guidance.

#### Custom Commands

To build your own custom command:

1. Create a C# POCO result type for `TResult` (optionally, use the Ollama Lame Attributes to annotate the model)
2. Implement `BasePromptCommand<TResult>` or `DbPromptCommand<TResult>`
3. Optionally create a custom request type inheriting from `PromptCommandRequest`
4. Override `Prompt(PromptCommandRequest request)` to generate prompt logic
5. Optionally override `getDefaultInstruction()` and/or `getPromptInstruction(request)`

### Ollama Lame Attributes

Instead of writting an instruction explaining the LLM what to output for the requested `TResult` or read it
from a DB, you can use the framework's OllamaAttributes to annotate the model at class and/or property level
to compose a more specific prompt to guide the model in its task (the DB approach is recommended though, since you cannot change
the attributes content once the application is compiled and deployed).

There are four types of OllamaAttribute:

- `OllamaJsonOutput`: this defines the expected output at class levels and has two fields available to fill:
      > Title: this should be a short description of the output goal, like ('Evaluation of X task and output of X business model').
      > Description: here you should explain what it expected to LLM to do in terms of output, reasoning steps... here you can be more extensive in the explanation of the task.
        
- `OllamaJsonProperty`: this defines a property of the model, they can be stacked and have two fields:
      > Title: this is a category name to allow the stacking of multiple annotations of the same 'type'. By default you shoud add a 'Description' category, but this is
               made this way so you can build any section to annotate your model according to your needs (you could build an 'Output Examples' section, for example).
      > Prop Description: this is the actual text that will be appended to your category. This should be a description of the field and what to expect.

- `OllamaRequirement` : this are 'special' attributes that will create their own section in the system message enforcing the model to follow specific rules.
                        They can be used at class or property level to add constraints.

- `OllamaJsonHint` : similar to the requirements attribute, this can be used at class or property level to guide the model about a specific part of the task
                     on how to generate a specific field. This are meant to serve as a support for the LLM with hints in the style of 'Review the user input and analyze the sources' 
                     or 'Pay attention to the customer tone to score this 'purchase_satisfaction' field'... use them when you want to add a part of the instruction that does not 
                     describe neither commands the LLM but advices or guides its reasoning process.

*Note: to make use of the Ollama Lame Attributes system your model MUST inherit from `StructuredOutput.cs`, otherwise the annotations won't be parse

Here is an example of an annotated structured output:

``` csharp

namespace Dotnet.OllamaSharp.LameChain.SDK.Command.Responses.StructuredOutputs
{
    [OllamaJsonOutput("Numerical answer for a query that can be answered with a decimal / float number according to the user intent and/or any specified instructions", Description = @"This schema contains a 'numeric_result' OR NULL in response to a given query. You MUST analyze the 
query to extract the intent, review any given instructions and reason if the query can be answered in numerical terms. In case it is, reason the problem to a numerical value that is logic and consistent with the analyzed intent. 
# IMPORTANT: In case the query CAN'T be answered in numerical terms,you MUST return null.")]
    [OllamaJsonRequirement("Output value MUST be a float number if the query intent can be answered in numerical terms OR NULL in case it can be not")]
    public class NumericResponse : StructuredOutput
    {
        [JsonPropertyName("numeric_result")]
        [OllamaJsonProperty(Title ="Description", PromptDescription = "A decimal or float value with the right logical response when it is possible to answer with a number, else null")]
        [OllamaJsonHint("Review carefully the input to reason if the request can be answered numerically or not to provide an accurate response that is compliant with the schema and the provided details about the expected output")]
        [OllamaJsonRequirement("- The answer MUST BE A FLOAT WHEN the proposed problem CAN be answered with a number. For example: { 'numeric_result' : 89.89 }")]
        [OllamaJsonRequirement("- The answer MUST BE NULL WHEN the proposed problem CANNOT be answered in numerical terms. For example: { 'numeric_result': null } ")]
        public float? Result { get; set; }
    }
}

```

## Chain Steps

A `ChainStep` stores:

- the command to run
- command configuration
- chaining configuration
- feeds and contextual data

It executes the command, processes the result, and passes outputs to the next step.

### Step types

#### `SingleThrowStep`

- the basic unit of work
- consumes one input
- produces one output
- the only step type that can execute the chain
- must end the chain or subchain

##### Junction

Use `.Join(...)` to merge split outputs into a single result.
A `JunctionStep` is itself a `SingleThrowStep`, so it can continue the chain or execute.

> Note: you cannot end a chain on a multi-throw step. Only `SingleThrowStep` and subclasses 
        like Junction step supports execution.

#### Stash

A stash is a type of step that is meant to be to execute some process and store the results as
a texts list to serve as source for other processes in the chain (for example, perform a similarity
search or augment a user query with an LLM)

A stash is not greedy by default (so the next one will read the stash as it would do with any other step) and 
it is isolated from the previous (so recieves no output 
from the previous in its prompt), but you can configure this params according to your needs.

#### Store

A Store is a type of step that is used to persist chain data. It can be the output of the chain or any 
intermediate step whose output you want to store immediately in case the chain fails to complete.

This type of steps will always deserialize the output of the previous step and will try to store it using the 
storing lambda passed on costruction and then forward the insert result to the next step.

#### Conditional

A conditional step inserts the configured step or subchain if a boolean Expression evaluates to true. This steps are useful to add simple branching logic
to your chain without using the LLM. They recieve an `Expression<Func<bool>>` as input param and execute the compiled expression during chain execution
to swap the alternate branch into the mainchain if the condition evaluates to true, else it continues the original main branch.

#### Smart Conditional

A conditional step executes a ScoredBoolEvaluation command to swap or not the configured 'TrueBranch' into the main chain or continueing
with the main chain if evaluates to false, passing along the justification comment to keep the chain context consistency. It is the 
'smart' equivalent to the ConditionalStep but using the LLM to perform the evaluation.

#### `MultiThrowStep`

`MultiThrowStep` is used for parallel or branched execution:

- `SplitterStep` (parallel outputs)
- `PipedStep` (same command over multiple inputs)
- `JunctionStep` (rejoin multiple outputs)

##### Splitter

A splitter takes one input and produces many outputs.
It can be used with `.Tap(...)` or `.SplitThrough(...)`.

##### Pipe

After splitting, use `.Pipe(...)` to execute a single command over multiple inputs.


### Step features

All steps also support feed configuration, allowing them to:

- pull data from previous steps (`FeedFrom`)
- inject extra context
- boost prompts with external sources

Steps are mutable and can clone or expand themselves via fluent methods.

Current step mutation methods:

```csharp

    public SingleThrowStep ExpandTo(IJsoneable command, StepSettings? stepSettings, string? feedForwardInstruction = null)
        => Activator.CreateInstance(typeof(SingleThrowStep), command, stepSettings, feedForwardInstruction) as SingleThrowStep;

    public SingleThrowStep ExpandTo<TCommand, TResult>(string instruction, CommandSettings commandSettings, StepSettings? settings, string? feedForwardInstruction = null) where TCommand : BasePromptCommand<TResult>, new()
        => ExpandTo(Activator.CreateInstance(typeof(TCommand), Commands.First().BorrowLlama, instruction, commandSettings) as IJsoneable, settings, feedForwardInstruction);

    public TStep ExpandTo<TStep>(IJsoneable command, StepSettings? request, string? feedForwardInstruction = null) where TStep : ChainStep
        => Activator.CreateInstance(typeof(TStep), command, request, feedForwardInstruction) as TStep;
    
    public TStep ExpandTo<TStep>(StepInstruction instruction) where TStep : ChainStep
        => Activator.CreateInstance(typeof(TStep), instruction) as TStep;

    public TStep ExpandTo<TStep, TCommand, TResult>(string instruction, CommandSettings commandSettings, StepSettings settings, string? feedForwardInstruction = null)
        where TCommand : BasePromptCommand<TResult>, new()
        where TStep : ChainStep
            => ExpandTo<TStep>(Activator.CreateInstance(typeof(TCommand), Commands.First().BorrowLlama, instruction, commandSettings) as IJsoneable, settings, feedForwardInstruction);

    public SplitterStep Plug(List<StepInstruction> instructions, StepSettings? request, string? splitterFeedFwd = null)
        => Activator.CreateInstance(typeof(SplitterStep), instructions, request, splitterFeedFwd) as SplitterStep;
    
    public SplitterStep SplitTo(StepInstruction splitted, List<StepInstruction> instructions)
        => Activator.CreateInstance(typeof(SplitterStep),  splitted, instructions) as SplitterStep;

    public StashedStep ToStash(StepInstruction instruction, bool isGreedy = false, bool isIsolated = true)
    {
        if (!instruction.Command.GetType().IsAssignableTo(typeof(SourceableCommand)))
            throw new InvalidOperationException($"{nameof(ChainStep)} >> {nameof(ToStash)} >> INVALID STEP CONFIGURATION >> The configured command type ({instruction.Command.GetType().Name}) is not a {typeof(SourceableCommand)} or any subclass of it");

        return Activator.CreateInstance(typeof(StashedStep), instruction.Command, instruction.StepSettings, isGreedy, isIsolated, instruction.FeedFwdInstruction) as StashedStep;
    }

    public ConditionalStep ToConditional(Expression<Func<bool>> condition, StepSettings stepSettings, string? feedFwd = null)
        => Activator.CreateInstance(typeof(ConditionalStep), condition, stepSettings, feedFwd) as ConditionalStep;

    public StoredStep<TStored> AsStore<TStored>(StepInstruction instruction) where TStored : class
        => Activator.CreateInstance(typeof(StoredStep<TStored>), instruction) as StoredStep<TStored>;

    public SmartConditionalStep ToSmartConditional(StepInstruction instruction)
    {
        if (instruction.Command.GetType() != typeof(ScoredBoolCommand) && !instruction.Command.GetType().IsSubclassOf(typeof(ScoredBoolCommand)))
            throw new InvalidDataException($"{nameof(SmartConditionalStep)} >> {instruction.Command.GetType().Name} >> A SmartConditionalStep command must be a ScoredBoolCommand or a subclass of it");

        return Activator.CreateInstance(typeof(SmartConditionalStep), instruction.Command, instruction.StepSettings, instruction.FeedFwdInstruction) as SmartConditionalStep;
    }

```

## Fluent Extensions

The LameChain fluent API helps you build chains that are valid and composable.
It exposes only the methods that make sense for the current step type.

### Basic chain API

```csharp

.StartWith(StepInstruction firstInstruction, CommandSettings defaultSettings, string? finalSysMessage = null, string? chainIntent = null)
.SubChainWith<TStep>(params object?[]? args)
.Then(this SingleThrowStep step, IJsoneable command, StepSettings request, string? feedFwdInstruction = null)
.ThenIf(this SingleThrowStep step, Expression<Func<bool>> condition, StepSettings conditionSettings, SingleThrowStep trueBranch, string? conditionFeedFwd = null)
.ThenIf<TStep>(this SingleThrowStep step, Expression<Func<bool>> condition, StepSettings conditionSettings, StepInstruction trueInstruction, string? conditionFeedFwd = null)
.ThenIf(this SingleThrowStep step, StepInstruction evaluator, SingleThrowStep trueBranch)
.ThenIf<TStep>(this SingleThrowStep step, StepInstruction evaluator, StepInstruction trueInstruction)
.Tap(this SingleThrowStep step, List<StepInstruction> instructions, StepSettings? plugSettings = null)
.SplitThrough(this SingleThrowStep step, StepInstruction splitted, List<StepInstruction> instructions)
.Pipe(this SplitterStep step, IJsoneable command, StepSettings pipedSettings, string? pipeFeedFwd = null)
.Join(this SplitterStep step, StepInstruction instruction)
.Store<TStored>(this SingleThrowStep step, StepInstruction storeInstruction)
.StoreIf<TPrev>(this SingleThrowStep step, Expression<Func<bool>> condition, StepSettings conditionSettings, StoredStep<TPrev> trueBranch, string? conditionFeedFwd = null)
.Stash(this SingleThrowStep step, StepInstruction stashInstruction, out Func<Guid> stashId, bool isGreedy = false, bool isIsolated = true)
.StashIf(this SingleThrowStep step, StepInstruction evaluator, StashedStep trueBranch)
.ForwardFirstType<TStep>(this ChainStep step)
.ExposeThisId(this SingleThrowStep step, out Guid id)
.WithRebujito(this SingleThrowStep | SplitterStep step, List<string> sources, string? guidance = null, int? feedDose = null)
.ChainFeedsFrom(this SplitterStep | SplitterStep step, List<Func<Guid>> steps, string? guidance = null)
.UseBroadcaster(this SingleThrowStep step, Action<Guid, string, LogLevel> broadcaster)
.ThenExecuteAsync(this SingleThrowStep step, bool withFinalMessage = false, bool withReplay = false, CommandSettings finalMsgSettings = null)
```

### Extension behavior

- `.StartWith(...)` begins a new chain
- `.SubChainWith(...)` begins a new sub chain
- `.UseBroadcaster(...)` adds a C# Action that will recieve any broadcasted event from the chain steps to log them or store
- `.Then(...)` continues a single-output chain
- `.Tap(...)` splits into parallel command branches
- `.SplitThrough(...)` fans one output into several commands
- `.Pipe(...)` runs a command over multiple outputs
- `.Join(...)` merges branch results into one output
- `.Stash(...)` adds a stashed step to use it as feed source in the chain
- `.Store(...)` adds a stored step to persist the previous output outside the chain
- `.ThenIf(...)` adds a conditional or smart conditional step that executes a branch if a boolean condition is met
- `.StashIf(...)` adds a stash step that can be subchained IF a boolean condition is passed, else continues the chain
- `.StoreIf(...)` adds a stored step that will be executed IF a boolean condition is passed
- `.ForwardFirstType<TStep>(...)` sets / returns the first step of a subchain. Use this to 'close' a subchain that starts with one TStep and ends with another type
- `.ExposeThisId(...)` captures step IDs for later feeding
- `.ExposeThisStep(...)` captures step for later feeding.
- `.WithRebujito(...)` boosts context with extra sources
- `.WithChainFeedsFrom(...)` configures a step feed by passing the step id's to read from
- `.ThenExecuteAsync(...)` finalizes execution and returns `ChainResult`

### Broadcast chain execution:

You surely will want to know what is happening during the chain execution (specially when it fails) and to do so, the framework
uses  'broadcaster'. A broadcaster is a C# Action that encapsulates a method with this signature:

``` csharp
    Task mthd(Guid stepId, string message, LogLevel level);
```

This action is registered and executed in the _runner, recieving the broadcasted information from steps & substeps and executing
whatever logic you want (logging, for example). This is done this way to avoid coupling the sdk with any logging library and to
allow more flexibility when processing the produced chain events.

#### Setup a broadcaster:


1 - Create a method with your chain event processing logic (in this case, logging)

```csharp

private void broadcaster(Guid runnerId, string message, LogLevel level)
{
    string logMessage = $"{level} >> CHAIN STEP {runnerId} >> {message}";

    switch (level)
    {
        case (LogLevel.Error):
        case (LogLevel.Critical):
            _logger.LogError(logMessage);
            break;
        case (LogLevel.Warning):
            _logger.LogWarning(logMessage);
            break;
        case (LogLevel.Information):
        case (LogLevel.Debug):
        case (LogLevel.Trace):
        default:
            _logger.LogInformation(logMessage);
            break;
    }
}

```

2 - Encapsulate it in a C# Action (use a helper method to get the action already configured)

``` csharp

 public Action<Guid, string, LogLevel> GetBroadcastAction()
    => new Action<Guid,string, LogLevel>(broadcaster);

```

3 - Use the Fluent API to register the action in the chain runner

``` csharp

var result = await LameChain
    .StartWith(new StepInstruction(
        _factory.GetCommand<MessagePromptCommand, ChatMessage>(systemMessage: request.SystemMessage, request.Settings),
        new StepSettings(new ChatCommandRequest(request.Prompt, request.ChatHistory))),
        request.Settings
    )
    .UseBroadcaster(GetBroadcastAction())
    //.Then(...)

```

## Example usage

This example demonstrates how you can use LameChain to split a relatively long processess that might be too heavy (in cognitive terms) for the LLM into
more short and focused tasks that will add their results iteratively to achieve a refined final output that should be better than the one you would achieve
with a single instruction (specially for 'dumb' local models).

The chain simulates a creative process to create character concepts for videogames, but I guess it could be used for a movie or serie script too.
It starts with a simple instruction to build the character in different sequential and paralles processes biased towards the desired styles with web search data


```csharp

public async Task<ChainResult> ParallelChainExample(ChainedPrompt request)
    => await LameChain
        .StartWith(new StepInstruction(
                _factory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                    instruction: "Generate a character name and assign it an age (the age might be a specific number or a rough string approximation).", //This should be the 'request.SystemMessage'
                    settings: request.Settings),
                settings: new StepSettings(new PromptCommandRequest(
                    message: "Generate a character for a game ambiented in Spain in the XVI century", // This should be the 'request.Prompt'
                    guidanceMessage: "You are a character concept creator for a videogames company" // bias the output of the first step by assigning it a role that makes it an expert in the topic.
                )),
                feedFwd: ""),
            defaultSettings: request.Settings, // default settings for all the chain. If you don't pass individual settings to a command, this will be used instead
            finalSysMessage: "Ensure you output your answer in old castillian spanish style, but be consistent with the provided context data.", //This is just to demonstrate how to use the Final Message. Let's say you are doing some tests about how should the character speak in the game
            chainIntent: "Create game character") // This will be passed to all steps so every LLM request has a clear idea of what is the final task / overall goal
        .UseBroadcaster(GetBroadcastAction())
        .ExposeThisId(out var startId)
        .Then(
            _factory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                instruction: "Reduce the previous output to ensure it only contains the requested name and age, remove the rest",
                request.Settings), // pass indiviual settings for each step if necessary to regulate how focused or creative is the LLM
            new StepSettings(new PromptCommandRequest("")),
            feedFwdInstruction: "Use the name and the age to develop your part of the character") // Help the next step with its task by passing 'Feed Forward' messages that will be added to the context along with the previous output.
        .ExposeThisId(out var thenId)
        .SplitThrough(
        //This step will split the chain but executing a command first that will feed every plugged substep.
        // The splitted step recieves the previous output (ideally only the name and age after the reduction) and generate some kind of descriptive 'picture' of a possible character that will be passed
        // to the parallel branches for further post-processing.
            new StepInstruction(
                _factory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                    instruction: "Generate a succinct 'iconic moment' for the character, that is: a description of a typical situation or scene for this character, a moment that should represent its nature and the way it is. Use no more than 50-60 words",
                    settings: request.Settings), // You could pass some very creative settings in this step for example
                settings: new StepSettings(new PromptCommandRequest(message: "")),
                feedFwd: "Use this character typical scene as a character concept to inspire your creations"), [
                    //Based on this little character concept (that inherits the name and age) the other steps will pick a faction, a game location as hometowm and generate an extensive background story to use it as a base for the final result (that will merge the three outputs)
                new StepInstruction(
                    _factory.GetStringChoiceCommand( //You can use different type of Lame Commands in the chain. Here I'm using an AtomicValue Command that selects a single string from the passed list based on the given instruction
                        guidanceMessage: "Select a faction from the available list for the game character you are creating. Use the provided data to select the faction that fits best or choose at random if none stands out."),
                    new StepSettings(new StringChoiceRequest(
                        choices: ["Germaners", "Comuneros", "Tercio Imperial" ],
                        message: "Select a game faction for the character", // Enforce the instruction if you get hallucinations with dumb models or maybe the context adds too much noise and misleads the LLM
                        isGuidanceAppend: true, // I'm using the command default instruction so I set this param to true to append the chain context 
                        model: null))
                        .FeedFrom(startId), // This is just to demonstrate an individual feed in a splitted step from a previous process different than the previous
                    feedFwd: "# IMPORTANT: the provided faction name MUST be the character faction"), // Help the next worker focus on its task when you start to add too much content to the LLM's context window (for example joining three outputs, like in the next step)
                new StepInstruction(
                    _factory.GetEnumChoiceCommand<EGameLocations>( // You can use other type of AtomicValue command for quick selections based on your app code. values  Here I'm using an AtomicValue command that selects an app enum value based on the instruction.
                        guidanceMessage: "Select a game location from the available list for the game character you are creating. Use the provided data to select the location that fits best with the profile."),
                    new StepSettings(
                        new PromptCommandRequest(
                            message: "Select a game location for the character as stated in your instruction", 
                            isGuidanceAppend: true)
                        ),
                    feedFwd: "# IMPORTANT: THIS IS THE CHARACTER PLACE OF ORIGIN. Use the selected location as the character's place of birth"), // Every parallel branch can add its own Feed Forward message to help the next step to understand / use the output of its instruction
                new StepInstruction(
                    _factory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                        instruction:"Generate a background story or profile for the character you are creating. Use the provided data to figure out what kind of character might appropriate in terms of style, mood, vibe..."),
                    settings: new StepSettings(new PromptCommandRequest(message: "", isGuidanceAppend : true))
                        .WithDataBoost( // Steps can be boosted by feeding the output of previous steps but also by adding external string sources.
                        // In this case I'm adding semi-random data to the bias the generated character base profile towards specific styles.
                        // But ideally you would add here well processed sources (in this example it could be real human-made character concepts made by the game studio artists
                            "Use the below data to bias your final response towards that style, ambience, topic or vibe",
                            await _langSearch.SearchWebTexts(new WebSearchRequest("Don Pablo o La vida del buscón. Lazarillo de Tormes, sinopsis.", 5), returnSnippet: false, resultsClamp: 100)),
                    feedFwd: "Use this profile as an inspiration for your final character profile, but adapt it to the game lore") // Guide the refining / summarizing step to leverage the output of the different branches and get more consistent results
            ])
        // Now that the chain has been splitted in 3 branches, it is possible to work on each branch indepently by piping commands that will be executed on each recieved previous output
        // This part of the example tries to demonstrate how to use another step to post-process the generated outputs and get more consistent results by generating
        // some semi-random content that is based on the generated content so far (the goal is to augment the previous results and have more base material to work with in the joining step)
        // Note that now every FeedForward message from the splitter will be 'spent' in this step, you have to use the pipeFeedFwd message to guide the next one
        .Pipe(
            _factory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                instruction: "Review the content so far and combine it with the provided sources in a very short story plot (around 50 words)",
                settings: request.Settings),
            pipedSettings: new StepSettings(new PromptCommandRequest(message: "", isGuidanceAppend: true)),
            pipeFeedFwd: "Review all the sources and get a consistent overview of the expected character profile. #IMPORTANT: use the character 'Faction' and game place of origin to generate your profile")
        // You can add rebujitos of data to influence the outputs of each branch of the pipe if you need.
        // In this case I'm simulating more style biasing without filtering it, but ideally you would add business content that you would like to apply on each branch.
        // In this example, it could be more content produced by the game studio staff (like scene scripts or even quest scripts, the idea
        // is to add get results that are aligned with the game lore so the final junction step does not hallucinate and add content from it's training dataset
        .WithRebujito(
            await _langSearch.SearchWebTexts(new WebSearchRequest("Revuelta de los Comuneros. Rebelion de las Germanias", results: 3), returnSnippet: false),
            guidance: "Use this data as a source of style references and add merge them in your final response along with the generated game lore. Output your response in always in English despite the source language.", // It is possible to add guidance instruction about the rebujito content to help the lLM to use it
            feedDose: 100)
        .Join(
            new StepInstruction(
                _factory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                    instruction: "Generate a full character profile (400-500 words) with the provided data. Review the character Faction and place of origin, you MUST include those in your profile. Include a background story, a psychological profile and a 'usual routines' section"),
                settings: new StepSettings(
                    new PromptCommandRequest(
                        message: "Generate the requested character profile",
                        isGuidanceAppend: true)) // Enforce the instruction (specially if you are using small local models and the context window is relatively filled)
                    .FeedFrom(thenId) // Feed the name and age to ensure it uses the one generated at this step (dumb models may have alucinated with the background story and the rebujito feeds)
            )
        )
        .ThenExecuteAsync(request.WithFinalMessage, request.WithReport, request.FinalMessageSettings ?? request.Settings);
```

This example demonstrates how can you use LameChain to build a 'smart' rag that will:

- Evaluate the user input to extract the user intent and know what is he trying to do / what is the user querying about
- Evaluate and score if the user intent is related to any of the available DB collections passed as choices
- In case it finds a related collection, creates a parallell branch that will:
    - 'Expand' the rag retrieval by generating an un-grounded answer to the query that will be used to cover more points in the similarity search and stash it.
    - 'Augment' the user query by generating N variants of the input to cover more points in the similarity search and stash it.
    - Perform a 'smart' similarity search that will use the LLM and the output of the scoring evaluator to select the best DB collection(s) to query them
      using the contents of the previous stashes to find more similar documents and / or more focused on the query topic, then stash the results.
- Execute a RagCommand ('configured' for Q&A tasks grounded to optional sources) that will recieve the result of the Similarity Search (if any) OR
  will try to answer the question with the LLM built-in knowledge from the training dataset


```csharp

    public async Task<ChainResult> SmartRagChain(SimpleCommandRequest request)
        => await LameChain
            .StartWith(new StepInstruction(
                _factory.GetCommand<UserIntentCommand, ChatMessage>(systemMessage: "", request.Settings),
                new StepSettings(new PromptCommandRequest(request.Prompt)),
                feedFwd: "Use the generated User Intent to guide you in your task"),
                request.Settings, //Default Settings for all the chains
                finalSysMessage: "", // There is no need to fill this since there is no user final message required
                chainIntent: "")
            .UseBroadcaster(GetBroadcastAction())
            .StashIf(new StepInstruction(
                _factory.GetCommand<ScoredBoolCommand, ScoredBoolResponse>(
                    systemMessage: $"Your task is to determine whether the provided User Intent is related to any of the Collections of the list below"),
                new StepSettings(new PromptCommandRequest(""))
                    .WithDataBoost("AVAILABLE COLLECTIONS:", await getChromaCollectionChoices(withChatCollections: false)),
                feedFwd: "Use this data as a reliable source to answer the user"),
                trueBranch: LameChain.SubChainWith<StashedStep>(
                    new StepInstruction(
                        _factory.GetSourceable<RagExpansionCommand>(),
                        new StepSettings(new RagExpansionRequest(expansions: 1, request.Prompt))
                    ),
                    false, //TODO: StashSettings : StepSettings & StepWithCustomParamsSettings : StepSettings y quitar esta guarrada
                    true
                    )
                .ExposeThisStep(out var ragExpansion)
                .Stash(new StepInstruction(
                    _factory.GetSourceable<QueryAugmentationCommand>(),
                    new StepSettings(new RagExpansionRequest(expansions: 3, request.Prompt, withFewShot: true, 2))),
                    out var queryAugmentId,
                    isGreedy: true,
                    isIsolated: true)
                .Stash(new StepInstruction(
                    _factory.GetEmbeddedSourceable<SmartQuerySourceable>(
                        _chromaService.SimilaritySearch,
                        llamaGuidance: "Select ONLY the 'COLLECTION NAME' value of the provided list OR empty list if there are no collections relevant for the user query."
                    ), // appended to Core Message (default | db)
                    new StepSettings(
                        new SmartQueryRequest(
                            request.Prompt,
                            collectionChoices: await getChromaCollectionChoices(withChatCollections: false), // add a formated catalogue of DB collections with descriptions and topics to help the LLM decide
                            maxChoices: 1,
                            resultsPerChoice: 3,
                            guidanceMessage: string.Empty, // this is overwritten by prev_ctx so leave it empty
                            dimensions: 512,
                            model: "nomic-embed-text",
                            filters: null),
                        withFullContext: false,
                        withPrevSchema: false
                        )// A nested feed is a feed that should go to a sub step or a sub command inside a step
                            // In this case Im passing the result of the ScoredBool (with the justification comment) to the MultiChoice command that selects the best available collections.
                            // This will help the LLM decide and will retur more accurate results when there are overlapping collection topics
                        .WithNestedFeed(nameof(MultiChoiceCommand), [ragExpansion.WhoIsPrevious], isForStep: false) //This is the only way of feeding a subranch nested COMMAND (not a step substep) from the owning step
                    ), 
                    out var smartQueryId)
                // Feed the VectorSearch command (which is the 'main' command of the SmartQueryCommand)
                // with the expansion stashes
                .ChainFeedsFrom([ragExpansion.GetRunnerId, queryAugmentId])
                // At this point you are working with the last subchain step
                // so you have to finish the subchain by passing the first step to be linked properly with the ConditionalStep
                // use this method to do that and cast it to the right type (this feature is still under development)
                .ForwardFirstType<StashedStep>() 
                )
            .Then(_factory.GetCommand<RagQueryCommand, ChatMessage>(systemMessage: request.SystemMessage),
                    new StepSettings(new ChatCommandRequest(request.Prompt, request.SystemMessage)))
            .ChainFeedsFrom([smartQueryId]) // retrieve the output of the SmartQueryCommand that made the query with the expansions
            .ThenExecuteAsync(withFinalMessage: false, withReplay: true);

```

Here are more simple (and conceptual) examples of usage for conditional steps and stores:

```csharp

public async Task<ChainResult> ConditionalChainExamples(CommandChatRequest request)
{           
    // Use Store<TPrevious> to retrieve and store the result of a step using a lambda with your required logic
    // Here I'm saving the new generated assistant message in Chroma
    var result = await LameChain
        .StartWith(new StepInstruction(
            _factory.GetCommand<MessagePromptCommand, ChatMessage>(systemMessage: request.SystemMessage, request.Settings),
            new StepSettings(new ChatCommandRequest(request.Prompt, request.ChatHistory))),
            request.Settings //Default Settings for all the chains
        )
        .UseBroadcaster(GetBroadcastAction()) // Add an Action with the right signature to process the broadcasted chain events (logging them or storing them)
        .Store<ChatMessage>(
            new StepInstruction(
                _factory.GetStoreable<ChatMessage>(_chromaService.OnNewChatMessage),
                new StepSettings(new StoreableCommandRequest<ChatMessage>(collectionName: $"ChatBot-{_apiSettings.DefaultUserName}"))
            )
        )
        .ThenExecuteAsync(withFinalMessage: false, withReplay: true);

    // Store a ChainStep result IF a boolean Expression is met (no LLM routing)
    result = await LameChain
        .StartWith(new StepInstruction(
            _factory.GetCommand<MessagePromptCommand, ChatMessage>(systemMessage: request.SystemMessage, request.Settings),
            new StepSettings(new ChatCommandRequest(request.Prompt, request.ChatHistory))),
            request.Settings, //Default Settings for all the chains
            finalSysMessage: "" // There is no need to fill neither this or the chainIntent since there is no user final message required
        )
        .UseBroadcaster(GetBroadcastAction())
        .ThenIf<StoredStep<ChatMessage>>(() => request.ChatHistory.Count > 10, // This is a simple example of how to use the Expressions to pass conditions that will be evaluated on chain execution to run the 'True' branch or not 
            new StepSettings(), // this are the conditional step settings. You probably won't need them in unless you are using the LLM in the lambda your are passing. You can use them to feed data as usual and use it in the step logic as needed
            new StepInstruction(
                _factory.GetStoreable<ChatMessage>(_chromaService.OnNewChatMessage), // In this example I'm just storing a chat conversation using Chroma without too much processing. Pass a function adapted to the chain model and your logic here
                new StepSettings(new StoreableCommandRequest<ChatMessage>(collectionName: $"ChatBot-{_apiSettings.DefaultUserName}"))
            )
        )
        .ThenExecuteAsync(withFinalMessage: false, withReplay: true);

    // This is a more explicit / readable usage of the Fluent API to work with StoredSteps.
    result = await LameChain
        .StartWith(new StepInstruction(
            _factory.GetCommand<MessagePromptCommand, ChatMessage>(systemMessage: request.SystemMessage, request.Settings),
            new StepSettings(new ChatCommandRequest(request.Prompt, request.ChatHistory))),
            request.Settings)
        .UseBroadcaster(GetBroadcastAction())
        .StoreIf<ChatMessage>(() => request.ChatHistory.Count > 10,
            new StepSettings(),
            new StepInstruction(
                _factory.GetStoreable<ChatMessage>(_chromaService.OnNewChatMessage),
                new StepSettings(new StoreableCommandRequest<ChatMessage>(collectionName: $"ChatBot-{_apiSettings.DefaultUserName}"))
            )
        )
        .ThenExecuteAsync(withFinalMessage: false, withReplay: true);

    return result;
}

```


## 🛠️ Sample Implementation

This repository includes a sample project: [**dotnet-llamasharp**](https://github.com/AEstradaGrech/dotnet-llamasharp)

It demonstrates how to use the SDK to gather data and feed a RAG (Retrieval-Augmented Generation) service, showcasing real-world integration.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.