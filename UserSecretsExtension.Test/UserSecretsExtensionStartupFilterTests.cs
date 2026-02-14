using Toolbelt.Blazor.WebAssembly.DevServer.Extensions.UserSecrets;

namespace UserSecretsExtension.Test;

public class UserSecretsExtensionStartupFilterTests
{
    [Test]
    public void MergeJsonStrings_SimpleOverride_OverridesScalarValue()
    {
        // Arrange
        var baseJson = """{"key1": "value1", "key2": "value2"}""";
        var overrideJson = """{"key2": "overridden"}""";

        // Act
        var result = UserSecretsExtensionStartupFilter.MergeJsonStrings(baseJson, overrideJson);

        // Assert
        var expected = """
        {
          "key1": "value1",
          "key2": "overridden"
        }
        """;
        result.Is(expected);
    }

    [Test]
    public void MergeJsonStrings_AddNewKey_AddsKeyFromOverride()
    {
        // Arrange
        var baseJson = """{"key1": "value1"}""";
        var overrideJson = """{"key2": "value2", "key3": "value3"}""";

        // Act
        var result = UserSecretsExtensionStartupFilter.MergeJsonStrings(baseJson, overrideJson);

        // Assert
        var expected = """
        {
          "key1": "value1",
          "key2": "value2",
          "key3": "value3"
        }
        """;
        result.Is(expected);
    }

    [Test]
    public void MergeJsonStrings_NestedObjects_MergesRecursively()
    {
        // Arrange
        var baseJson = """{"parent": {"child1": "value1", "child2": "value2"}}""";
        var overrideJson = """{"parent": {"child2": "overridden", "child3": "value3"}}""";

        // Act
        var result = UserSecretsExtensionStartupFilter.MergeJsonStrings(baseJson, overrideJson);

        // Assert
        var expected = """
        {
          "parent": {
            "child1": "value1",
            "child2": "overridden",
            "child3": "value3"
          }
        }
        """;
        result.Is(expected);
    }

    [Test]
    public void MergeJsonStrings_ObjectReplacedByScalar_ReplacesEntireObject()
    {
        // Arrange
        var baseJson = """{"config": {"nested1": "value1", "nested2": "value2"}}""";
        var overrideJson = """{"config": "simple-string"}""";

        // Act
        var result = UserSecretsExtensionStartupFilter.MergeJsonStrings(baseJson, overrideJson);

        // Assert
        var expected = """
        {
          "config": "simple-string"
        }
        """;
        result.Is(expected);
    }

    [Test]
    public void MergeJsonStrings_ScalarReplacedByObject_ReplacesWithObject()
    {
        // Arrange
        var baseJson = """{"config": "simple-string"}""";
        var overrideJson = """{"config": {"nested1": "value1", "nested2": "value2"}}""";

        // Act
        var result = UserSecretsExtensionStartupFilter.MergeJsonStrings(baseJson, overrideJson);

        // Assert
        var expected = """
        {
          "config": {
            "nested1": "value1",
            "nested2": "value2"
          }
        }
        """;
        result.Is(expected);
    }

    [Test]
    public void MergeJsonStrings_ArrayValues_ReplacesEntireArray()
    {
        // Arrange
        var baseJson = """{"items": ["item1", "item2", "item3"]}""";
        var overrideJson = """{"items": ["newItem1", "newItem2"]}""";

        // Act
        var result = UserSecretsExtensionStartupFilter.MergeJsonStrings(baseJson, overrideJson);

        // Assert
        var expected = """
        {
          "items": [
            "newItem1",
            "newItem2"
          ]
        }
        """;
        result.Is(expected);
    }

    [Test]
    public void MergeJsonStrings_ComplexNestedStructure_MergesCorrectly()
    {
        // Arrange
        var baseJson = """
        {
          "database": {
            "connection": {
              "host": "localhost",
              "port": 5432
            },
            "credentials": {
              "username": "admin"
            }
          },
          "logging": {
            "level": "info"
          }
        }
        """;
        var overrideJson = """
        {
          "database": {
            "connection": {
              "port": 5433,
              "ssl": true
            },
            "credentials": {
              "password": "secret"
            }
          },
          "cache": {
            "enabled": true
          }
        }
        """;

        // Act
        var result = UserSecretsExtensionStartupFilter.MergeJsonStrings(baseJson, overrideJson);

        // Assert
        var expected = """
        {
          "database": {
            "connection": {
              "host": "localhost",
              "port": 5433,
              "ssl": true
            },
            "credentials": {
              "username": "admin",
              "password": "secret"
            }
          },
          "logging": {
            "level": "info"
          },
          "cache": {
            "enabled": true
          }
        }
        """;
        result.Is(expected);
    }

    [Test]
    public void MergeJsonStrings_EmptyBase_ReturnsOverride()
    {
        // Arrange
        var baseJson = """{}""";
        var overrideJson = """{"key1": "value1", "key2": "value2"}""";

        // Act
        var result = UserSecretsExtensionStartupFilter.MergeJsonStrings(baseJson, overrideJson);

        // Assert
        var expected = """
        {
          "key1": "value1",
          "key2": "value2"
        }
        """;
        result.Is(expected);
    }

    [Test]
    public void MergeJsonStrings_EmptyOverride_ReturnsBase()
    {
        // Arrange
        var baseJson = """{"key1": "value1", "key2": "value2"}""";
        var overrideJson = """{}""";

        // Act
        var result = UserSecretsExtensionStartupFilter.MergeJsonStrings(baseJson, overrideJson);

        // Assert
        var expected = """
        {
          "key1": "value1",
          "key2": "value2"
        }
        """;
        result.Is(expected);
    }

    [Test]
    public void MergeJsonStrings_NumericValues_OverridesCorrectly()
    {
        // Arrange
        var baseJson = """{"count": 10, "price": 99.99, "active": true}""";
        var overrideJson = """{"count": 20, "price": 149.99, "active": false}""";

        // Act
        var result = UserSecretsExtensionStartupFilter.MergeJsonStrings(baseJson, overrideJson);

        // Assert
        var expected = """
        {
          "count": 20,
          "price": 149.99,
          "active": false
        }
        """;
        result.Is(expected);
    }

    [Test]
    public void MergeJsonStrings_NullValues_HandlesNullCorrectly()
    {
        // Arrange
        var baseJson = """{"key1": "value1", "key2": null}""";
        var overrideJson = """{"key2": "value2", "key3": null}""";

        // Act
        var result = UserSecretsExtensionStartupFilter.MergeJsonStrings(baseJson, overrideJson);

        // Assert
        var expected = """
        {
          "key1": "value1",
          "key2": "value2",
          "key3": null
        }
        """;
        result.Is(expected);
    }

    [Test]
    public void MergeJsonStrings_ArrayOfObjects_ReplacesCompleteArray()
    {
        // Arrange
        var baseJson = """{"users": [{"name": "Alice", "age": 30}, {"name": "Bob", "age": 25}]}""";
        var overrideJson = """{"users": [{"name": "Charlie", "age": 35}]}""";

        // Act
        var result = UserSecretsExtensionStartupFilter.MergeJsonStrings(baseJson, overrideJson);

        // Assert
        var expected = """
        {
          "users": [
            {
              "name": "Charlie",
              "age": 35
            }
          ]
        }
        """;
        result.Is(expected);
    }

    [Test]
    public void MergeJsonStrings_DeepNestedObjects_MergesAtAllLevels()
    {
        // Arrange
        var baseJson = """
        {
          "level1": {
            "level2": {
              "level3": {
                "value": "original"
              }
            }
          }
        }
        """;
        var overrideJson = """
        {
          "level1": {
            "level2": {
              "level3": {
                "value": "overridden",
                "newValue": "added"
              }
            }
          }
        }
        """;

        // Act
        var result = UserSecretsExtensionStartupFilter.MergeJsonStrings(baseJson, overrideJson);

        // Assert
        var expected = """
        {
          "level1": {
            "level2": {
              "level3": {
                "value": "overridden",
                "newValue": "added"
              }
            }
          }
        }
        """;
        result.Is(expected);
    }

    [Test]
    public void MergeJsonStrings_MixedDataTypes_HandlesAllTypes()
    {
        // Arrange
        var baseJson = """
        {
          "string": "text",
          "number": 42,
          "decimal": 3.14,
          "boolean": true,
          "null": null,
          "array": [1, 2, 3],
          "object": {"key": "value"}
        }
        """;
        var overrideJson = """
        {
          "string": "updated",
          "number": 100,
          "decimal": 2.71,
          "boolean": false,
          "array": [4, 5],
          "object": {"newKey": "newValue"}
        }
        """;

        // Act
        var result = UserSecretsExtensionStartupFilter.MergeJsonStrings(baseJson, overrideJson);

        // Assert
        var expected = """
        {
          "string": "updated",
          "number": 100,
          "decimal": 2.71,
          "boolean": false,
          "null": null,
          "array": [
            4,
            5
          ],
          "object": {
            "key": "value",
            "newKey": "newValue"
          }
        }
        """;
        result.Is(expected);
    }
}
