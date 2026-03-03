using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Framework.Configurations.Tests;

[TestFixture]
public class ValueAppearsInListAttributeTests
{
    public class TestValueAppearsInList
    {
        [ValueAppearsInList(nameof(DemoItem.ListValue), nameof(DemoItem.CheckValue))]
        public List<DemoItem> Items { get; set; } = new();
    }
    
    // Demo class for testing
    public class DemoItem
    {
        public int? ListValue { get; set; }
        public int? CheckValue { get; set; }
    }

    private static IEnumerable<TestCaseData> TestIsValidCaseData()
    {
        // Case 1: CheckValue exists in ListValue → should pass
        yield return new TestCaseData(new List<DemoItem>
        {
            new() { ListValue = 1, CheckValue = 1 }
        }).SetName("CheckValueExistsInList_Pass");

        // Case 2: CheckValue exists in list (multiple items)
        yield return new TestCaseData(new List<DemoItem>
        {
            new() { ListValue = 1, CheckValue = 1 },
            new() { ListValue = 2, CheckValue = 2 }
        }).SetName("MultipleCheckValuesExist_Pass");

        // Case 3: CheckValue is null → should pass (null is skipped)
        yield return new TestCaseData(new List<DemoItem>
        {
            new() { ListValue = 1, CheckValue = null }
        }).SetName("CheckValueIsNull_Pass");

        // Case 4: ListValue is null, but CheckValue is null → passes (null is skipped)
        yield return new TestCaseData(new List<DemoItem>
        {
            new() { ListValue = null, CheckValue = null }
        }).SetName("BothNull_Pass");
    }

    [Test, TestCaseSource(nameof(TestIsValidCaseData))]
    public void TestIsValid_WhenCheckValueExistsInList_ShouldPassValidation(List<DemoItem> items)
    {
        // Arrange
        var obj = new TestValueAppearsInList { Items = items };
        var validationContext = new ValidationContext(obj);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(obj, validationContext, validationResults, validateAllProperties: true);

        // Assert
        Assert.True(isValid, "Validation should pass when CheckValue is in ListValue or is null.");
        Assert.IsEmpty(validationResults, "No validation errors should be returned.");
    }

    private static IEnumerable<TestCaseData> TestIsNotValidCaseData()
    {
        // Case 1: CheckValue not in ListValue
        yield return new TestCaseData(new List<DemoItem>
        {
            new() { ListValue = 1, CheckValue = 2 }
        }).SetName("CheckValueNotInList_Fail");

        // Case 2: ListValue is null, CheckValue is non-null → fails
        yield return new TestCaseData(new List<DemoItem>
        {
            new() { ListValue = null, CheckValue = 1 }
        }).SetName("ListValueNullCheckValueNotNull_Fail");

        // Case 3: Multiple items, one CheckValue not in list
        yield return new TestCaseData(new List<DemoItem>
        {
            new() { ListValue = 1, CheckValue = 1 },
            new() { ListValue = 2, CheckValue = 3 } // 3 not in [1,2]
        }).SetName("OneCheckValueNotInList_Fail");

        // Case 4: ListValue is null, CheckValue is non-null (multiple)
        yield return new TestCaseData(new List<DemoItem>
        {
            new() { ListValue = null, CheckValue = 1 },
            new() { ListValue = null, CheckValue = 2 }
        }).SetName("MultipleListNullCheckValueNotNull_Fail");
    }

    [Test, TestCaseSource(nameof(TestIsNotValidCaseData))]
    public void TestIsNotValid_WhenCheckValueNotInList_ShouldFailValidationWith1Error(List<DemoItem> items)
    {
        // Arrange
        var obj = new TestValueAppearsInList { Items = items };
        var validationContext = new ValidationContext(obj);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(obj, validationContext, validationResults, validateAllProperties: true);

        // Assert
        Assert.That(isValid, Is.False, "Validation should fail when CheckValue is not in ListValue.");
        Assert.That(validationResults.Count, Is.EqualTo(1), "Exactly one validation error expected.");
    }
}
