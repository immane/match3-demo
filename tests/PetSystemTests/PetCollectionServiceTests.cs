using Xunit;
using Match3Demo;

namespace Match3Demo.Tests;

public class PetCollectionServiceTests
{
    private PetCollectionService CreateService()
    {
        var dataSource = new FakePetDataSource();
        dataSource.AddPet(TestData.CreatePetDef("cat_01"));
        dataSource.AddPet(TestData.CreatePetDef("dog_01", PetRarity.Epic));
        var eventBus = new FakeEventBus();
        return new PetCollectionService(dataSource, eventBus);
    }

    [Fact]
    public void AddPet_ReturnsInstanceWithCorrectDefId()
    {
        var ds = new FakePetDataSource();
        ds.AddPet(TestData.CreatePetDef("cat_sleepy"));
        var eventBus = new FakeEventBus();
        var service = new PetCollectionService(ds, eventBus);

        var pet = service.AddPet("cat_sleepy");
        Assert.Equal("cat_sleepy", pet.PetDefId);
        Assert.NotEmpty(pet.Id);
    }

    [Fact]
    public void AddPet_EmitsPetAcquiredSignal()
    {
        var ds = new FakePetDataSource();
        ds.AddPet(TestData.CreatePetDef("cat_01"));
        var eventBus = new FakeEventBus();
        var service = new PetCollectionService(ds, eventBus);

        service.AddPet("cat_01");
        Assert.Contains("PetAcquired", eventBus.EmittedSignals);
    }

    [Fact]
    public void HasPet_AfterAdding_ReturnsTrue()
    {
        var ds = new FakePetDataSource();
        ds.AddPet(TestData.CreatePetDef("cat_01"));
        var eventBus = new FakeEventBus();
        var service = new PetCollectionService(ds, eventBus);

        service.AddPet("cat_01");
        Assert.True(service.HasPet("cat_01"));
    }

    [Fact]
    public void GetDuplicateCount_ThreeCopies_Returns3()
    {
        var ds = new FakePetDataSource();
        ds.AddPet(TestData.CreatePetDef("cat_01"));
        var eventBus = new FakeEventBus();
        var service = new PetCollectionService(ds, eventBus);

        service.AddPet("cat_01");
        service.AddPet("cat_01");
        service.AddPet("cat_01");
        Assert.Equal(3, service.GetDuplicateCount("cat_01"));
    }

    [Fact]
    public void AddXP_WithEnoughXP_LevelsUpAndEmitsSignal()
    {
        var ds = new FakePetDataSource();
        ds.AddPet(TestData.CreatePetDef("cat_01"));
        var eventBus = new FakeEventBus();
        var service = new PetCollectionService(ds, eventBus);

        var pet = service.AddPet("cat_01");
        int gained = service.AddXP(pet.Id, 1000);
        Assert.True(gained > 0);
        Assert.Contains("PetLeveledUp", eventBus.EmittedSignals);
    }

    [Fact]
    public void SetActivePet_UpdatesAndEmitsSignal()
    {
        var ds = new FakePetDataSource();
        ds.AddPet(TestData.CreatePetDef("cat_01"));
        var eventBus = new FakeEventBus();
        var service = new PetCollectionService(ds, eventBus);

        var pet = service.AddPet("cat_01");
        service.SetActivePet(pet.Id);
        Assert.Equal(pet.Id, service.GetActivePetId());
        Assert.Contains("ActivePetChanged", eventBus.EmittedSignals);
    }

    [Fact]
    public void SetFavorite_TogglesCorrectly()
    {
        var ds = new FakePetDataSource();
        ds.AddPet(TestData.CreatePetDef("cat_01"));
        var eventBus = new FakeEventBus();
        var service = new PetCollectionService(ds, eventBus);

        var pet = service.AddPet("cat_01");
        Assert.True(service.SetFavorite(pet.Id, true));
        Assert.True(service.GetPet(pet.Id)!.IsFavorite);
        Assert.True(service.SetFavorite(pet.Id, false));
        Assert.False(service.GetPet(pet.Id)!.IsFavorite);
    }
}
