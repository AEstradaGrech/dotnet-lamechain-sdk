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

#### Conditional Step

A conditional step executes a ScoredBoolEvaluation command to swap or not the configured 'TrueBranch' into the main chain or continueing
with the main chain if evaluates to false, passing along the justification comment to keep the chain context consistency

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

Example mutation methods:

```csharp
// Expand is for SingleThrowSteps
public SingleThrowStep ExpandTo(IJsoneable command, StepSettings? stepSettings, string? feedForwardInstruction = null)
    => Activator.CreateInstance(typeof(SingleThrowStep), command, stepSettings, feedForwardInstruction) as SingleThrowStep;

public SingleThrowStep ExpandTo<TCommand, TResult>(string instruction, CommandSettings commandSettings, StepSettings? settings, string? feedForwardInstruction = null)
    where TCommand : BasePromptCommand<TResult>, new()
    => ExpandTo(Activator.CreateInstance(typeof(TCommand), Commands.First().BorrowLlama, instruction, commandSettings) as IJsoneable, settings, feedForwardInstruction);

public TStep ExpandTo<TStep>(IJsoneable command, StepSettings? request, string? feedForwardInstruction = null)
    where TStep : ChainStep
    => Activator.CreateInstance(typeof(TStep), command, request, feedForwardInstruction) as TStep;

public TStep ExpandTo<TStep, TCommand, TResult>(string instruction, CommandSettings commandSettings, StepSettings settings, string? feedForwardInstruction = null)
    where TCommand : BasePromptCommand<TResult>, new()
    where TStep : ChainStep
    => ExpandTo<TStep>(Activator.CreateInstance(typeof(TCommand), Commands.First().BorrowLlama, instruction, commandSettings) as IJsoneable, settings, feedForwardInstruction);

// for Tap() chained steps
public SplitterStep Plug(List<StepInstruction> instructions, StepSettings? request, string? splitterFeedFwd = null)
  => Activator.CreateInstance(typeof(SplitterStep), instructions, request, splitterFeedFwd) as SplitterStep;

// for SplitThrough() chained steps
public SplitterStep SplitTo(StepInstruction splitted, List<StepInstruction> instructions)
  => Activator.CreateInstance(typeof(SplitterStep), splitted, instructions) as SplitterStep;

// For piped steps
public TStep ExpandTo<TStep>(StepInstruction instruction) where TStep : ChainStep
    => Activator.CreateInstance(typeof(TStep), instruction) as TStep;

public StashedStep ToStash(StepInstruction instruction, bool isGreedy = false, bool isIsolated = true)
{
    if (!instruction.Command.GetType().IsAssignableTo(typeof(SourceableCommand)))
        throw new InvalidOperationException($"{nameof(ChainStep)} >> {nameof(ToStash)} >> INVALID STEP CONFIGURATION >> The configured command type ({instruction.Command.GetType().Name}) is not a {typeof(SourceableCommand)} or any subclass of it");

    return Activator.CreateInstance(typeof(StashedStep), instruction.Command, instruction.StepSettings, isGreedy, isIsolated, instruction.FeedFwdInstruction) as StashedStep;
}

public ConditionalStep AsConditional(StepInstruction instruction)
{
    if (instruction.Command.GetType() != typeof(ScoredBoolCommand) && !instruction.Command.GetType().IsSubclassOf(typeof(ScoredBoolCommand)))
        throw new InvalidDataException($"{nameof(ConditionalStep)} >> {instruction.Command.GetType().Name} >> A ConditionalStep command must be a ScoredBoolCommand or a subclass of it");

    return Activator.CreateInstance(typeof(ConditionalStep), instruction.Command, instruction.StepSettings, instruction.FeedFwdInstruction) as ConditionalStep;
}
```

## Fluent Extensions

The LameChain fluent API helps you build chains that are valid and composable.
It exposes only the methods that make sense for the current step type.

### Basic chain API

```csharp
.StartWith(StepInstruction firstInstruction, CommandSettings defaultSettings, string? finalSysMessage = null, string? chainIntent = null)
.Then(this SingleThrowStep step, IJsoneable command, StepSettings request, string? feedFwdInstruction = null)
.Tap(this SingleThrowStep step, List<StepInstruction> instructions, StepSettings? plugSettings = null)
.SplitThrough(this SingleThrowStep step, StepInstruction splitted, List<StepInstruction> instructions)
.Pipe(this SplitterStep step, IJsoneable command, StepSettings pipedSettings, string? pipeFeedFwd = null)
.Join(this SplitterStep step, StepInstruction instruction)
.ExposeThisId(this SingleThrowStep step, out Guid id)
.WithRebujito(this SingleThrowStep | SplitterStep step, List<string> sources, string? guidance = null, int? feedDose = null)
.ThenExecuteAsync(this SingleThrowStep step, bool withFinalMessage = false, bool withReplay = false, CommandSettings finalMsgSettings = null)
```

### Extension behavior

- `.StartWith(...)` begins a new chain
- `.SubChainWith(...)` begins a new sub chain
- `.Then(...)` continues a single-output chain
- `.Tap(...)` splits into parallel command branches
- `.SplitThrough(...)` fans one output into several commands
- `.Pipe(...)` runs a command over multiple outputs
- `.Join(...)` merges branch results into one output
- `.Stash(...)` adds a stashed step to use it as feed source in the chain
- `.StashIf(...)` adds a stash step that can be subchained IF a ScoredBoolCommand condition is passed, else continues the chain
- `.ExposeThisId(...)` captures step IDs for later feeding
- `.ExposeThisStep(...)` captures step for later feeding.
- `.WithRebujito(...)` boosts context with extra sources
- `.WithChainFeedsFrom(...)` configures a step feed by passing the step id's to read from
- `.ThenExecuteAsync(...)` finalizes execution and returns `ChainResult`

## Example usage

This example demonstrates how you can use LameChain to split a relatively long processess that might be too heavy (in cognitive terms) for the LLM into
more short and focused tasks that will add their results iteratively to achieve a refined final output that should be better than the one you would achieve
with a single instruction (specially for 'dumb' local models).

The chain simulates a creative process to create character concepts for videogames, but I guess it could be used for a movie or serie script too.
It starts with a simple instruction to build the character in different sequential and paralles processes biased towards the desired styles with web search data


```csharp
return await LameChain
    .StartWith(new StepInstruction(
            _promptsFactory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                instruction: "Generate a character name and assign it an age (the age might be a specific number or a rough string approximation).",
                settings: request.Settings),
            settings: new StepSettings(new PromptCommandRequest(
                message: request.Prompt,
                guidanceMessage: request.SystemMessage
            )),
            feedFwd: ""),
        defaultSettings: request.Settings,
        finalSysMessage: request.FinalSystemMessage,
        chainIntent: "Create game character")
    .ExposeThisId(out var startId)
    .Then(
        _promptsFactory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
            instruction: "Reduce the previous output to ensure it only contains the requested name and age, remove the rest",
            request.Settings),
        new StepSettings(new PromptCommandRequest("")),
        feedFwdInstruction: "Use the name and the age to develop your part of the character")
    .ExposeThisId(out var thenId)
    .SplitThrough(
        new StepInstruction(
            _promptsFactory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                instruction: "Generate a succinct 'iconic moment' for the character, that is: a description of a typical situation or scene for this character, a moment that should represent its nature and the way it is. Use no more than 50-60 words",
                settings: request.Settings),
            settings: new StepSettings(new PromptCommandRequest(message: "")),
            feedFwd: "Use this character typical scene as a character concept to inspire your creations"),
        new List<StepInstruction> {
            new StepInstruction(
                _promptsFactory.GetStringChoiceCommand(
                    guidanceMessage: "Select a faction from the available list for the game character you are creating. Use the provided data to select the faction that fits best or choose at random if none stands out."),
                new StepSettings(new StringChoiceRequest(
                    choices: [ "Game Faction A", "Game Faction B", "Game Faction C" ],
                    message: "Select a game faction for the character",
                    settings: null,
                    model: null))
                    .FeedFrom(startId),
                feedFwd: "Use this game related data to ground your profile to the game lore"),
            new StepInstruction(
                _promptsFactory.GetEnumChoiceCommand<EGameLocations>(
                    guidanceMessage: "Select a game location from the available list for the game character you are creating. Use the provided data to select the location that fits best with the profile."),
                new StepSettings(new PromptCommandRequest(message: "Select a game location for the character as stated in your instruction")),
                feedFwd: "Use the selected location as the character's place of birth"),
            new StepInstruction(
                _promptsFactory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                    instruction: request.Instructions.Skip(4).First().SystemMessage),
                settings: new StepSettings(new PromptCommandRequest(message: "Generate a background story or profile for the character you are creating. Use the provided data to figure out what kind of character may be appropriate in terms of style, mood, vibe..."))
                    .WithDataBoost(
                        "Use the below data to bias your final response towards that style, ambience, topic or vibe",
                        await _langSearch.SearchRankedTexts(new RankedPageRequest { Count = 6, Query = "Bujias Campanolo, El Dia de la Bestia" }, returnSnippet: true)),
                feedFwd: "Use this profile as an inspiration for your final character profile, but adapt it to the game lore")
        })
    .Pipe(
        _promptsFactory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
            instruction: "Review the content so far and combine it with the provided sources in a very short story plot (around 50 words)",
            settings: request.Settings),
        pipedSettings: new StepSettings(new PromptCommandRequest(message: "")),
        pipeFeedFwd: "Review all the sources and get a consistent overview of the expected character profile.")
    .WithRebujito(
        await _langSearch.SearchRankedTexts(new RankedPageRequest { Count = 3, Query = "H.P Lovecraft stories. Richard Bachman novels" }, returnSnippet: false),
        guidance: "",
        feedDose: 100)
    .Join(
        new StepInstruction(
            _promptsFactory.GetAsJsoneable<MessagePromptCommand, ChatMessage>(
                instruction: "Generate a full character profile (400-500 words) with the provided data. Include a background story, a psychological profile and a 'usual routines' section"),
            settings: new StepSettings(new PromptCommandRequest(message: "Generate the requested character profile")).FeedFrom(thenId),
            feedFwd: ""))
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
                        false,
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
                                collectionChoices: await getChromaCollectionChoices(withChatCollections: false),
                                maxChoices: 1,
                                resultsPerChoice: 3,
                                guidanceMessage: string.Empty, // this is overwritten by prev_ctx so leave it empty
                                dimensions: 512,
                                model: "nomic-embed-text",
                                filters: null),
                            withFullContext: false,
                            withPrevSchema: false
                            ).WithNestedFeed(nameof(MultiChoiceCommand), [ragExpansion.WhoIsPrevious], isForStep: false) //This is the only way of feeding a subranch nested COMMAND (not a step substep) from the owning step
                        ), //Chroma metadata filters 
                        out var smartQueryId)
                    .ChainFeedsFrom([ragExpansion.GetRunnerId, queryAugmentId])
                    .ForwardFirstType<StashedStep>()
                 )
                .Then(_factory.GetCommand<RagQueryCommand, ChatMessage>(systemMessage: request.SystemMessage),
                      new StepSettings(new PromptCommandRequest(request.Prompt)))
                .ChainFeedsFrom([smartQueryId])
                .ThenExecuteAsync(withFinalMessage: false, withReplay: true);

```
