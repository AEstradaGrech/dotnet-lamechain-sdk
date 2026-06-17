# Copilot Instructions

## Project Guidelines
- Unit test best practices for this codebase:
1. ALL external dependencies must be MOCKED - no actual service instantiation
2. Use Mock<T> from Moq to mock all dependencies (IOllamaApiClient, IOptions<T>, etc.)
3. Configure mocks with Setup() to return dummy data that represents the expected return type
4. Only test the class under test in isolation (Unit tests vs Integration tests)
5. Use FluentAssertions for readable assertions
6. Mock IAsyncEnumerable return types properly with helper methods like GetAsyncEnumerable()
7. Test happy path and critical failure scenarios (KO paths)