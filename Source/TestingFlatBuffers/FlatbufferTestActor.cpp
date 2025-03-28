#include "FlatbufferTestActor.h"

// 1) Base FlatBuffers headers
#include "flatbuffers/flatbuffer_builder.h"
#include "flatbuffers/verifier.h"

// 2) Your generated code. Make sure the filename matches the one actually produced by flatc.
#include "FCheckFlatBuffer_generated.h" 
// or if you have an aggregator, "FlatBufferAutoIncludes.h", etc. 
// but that file must genuinely include `FCheckFlatBuffer_generated.h`

// Bring your nested namespaces into scope (this is optional but convenient)
using namespace TestingFlatBuffers::FCheckFlatBuffer;

AFlatbufferTestActor::AFlatbufferTestActor()
{
    PrimaryActorTick.bCanEverTick = true;
}

void AFlatbufferTestActor::BeginPlay()
{
    Super::BeginPlay();

    // 1) Call our basic demonstration
    MyBasicFlatBufferTest();

    // 2) Call advanced usage
    MyAdvancedFlatBufferTest();
}

void AFlatbufferTestActor::Tick(float DeltaTime)
{
    Super::Tick(DeltaTime);
}

/** Demonstrates basic building, verifying, and reading of a 'FCheckFlatBuffer' */
void AFlatbufferTestActor::MyBasicFlatBufferTest()
{
    // 1) Build a FlatBuffer
    flatbuffers::FlatBufferBuilder builder;

    // Create some example data
    auto nameOffset = builder.CreateString("UnrealMonster_Basic");
    std::vector<uint8_t> inventoryData{ 10, 20, 30, 40, 50 };
    auto inventoryOffset = builder.CreateVector(inventoryData);

    // Build the 'FCheckFlatBuffer'
    auto fbOffset = CreateFCheckFlatBuffer(
        builder,
        /*mana=*/300,
        /*hp=*/200,
        nameOffset,
        inventoryOffset,
        ECharacterType_Player
    );

    // Mark it as the root
    FinishFCheckFlatBufferBuffer(builder, fbOffset);

    // 2) Verify
    const uint8_t* bufferPointer = builder.GetBufferPointer();
    const size_t bufferSize = builder.GetSize();

    flatbuffers::Verifier verifier(bufferPointer, bufferSize);
    if (!VerifyFCheckFlatBufferBuffer(verifier))
    {
        UE_LOG(LogTemp, Error, TEXT("[MyBasicFlatBufferTest] Verification failed!"));
        return;
    }

    // 3) Access
    auto rootObj = GetFCheckFlatBuffer(bufferPointer);
    if (!rootObj)
    {
        UE_LOG(LogTemp, Error, TEXT("[MyBasicFlatBufferTest] Could not get root object!"));
        return;
    }

    // Extract fields
    int32 manaVal = rootObj->mana();
    int32 hpVal = rootObj->hp();
    auto  namePtr = rootObj->name();
    auto  inv = rootObj->inventory();
    auto  charType = rootObj->chtype();

    FString nameStr = (namePtr) ? UTF8_TO_TCHAR(namePtr->c_str()) : TEXT("(no name)");

    UE_LOG(LogTemp, Display, TEXT("=== MyBasicFlatBufferTest ==="));
    UE_LOG(LogTemp, Display, TEXT("Name: %s"), *nameStr);
    UE_LOG(LogTemp, Display, TEXT("Mana: %d"), manaVal);
    UE_LOG(LogTemp, Display, TEXT("HP:   %d"), hpVal);
    UE_LOG(LogTemp, Display, TEXT("Type: %d"), (int32)charType);

    if (inv)
    {
        FString invList;
        for (uint32 i = 0; i < inv->size(); ++i)
        {
            invList.AppendInt(inv->Get(i));
            if (i < inv->size() - 1) invList.Append(TEXT(", "));
        }
        UE_LOG(LogTemp, Display, TEXT("Inventory: [%s]"), *invList);
    }
    else
    {
        UE_LOG(LogTemp, Display, TEXT("No inventory!"));
    }

    // Optionally, let’s see what in-place mutation looks like:
    // We'll directly call a helper that modifies the buffer in memory:
    MutateFCheckBufferTest(const_cast<uint8_t*>(bufferPointer), bufferSize);
}

/** Demonstrates more advanced usage: object API, building nested structures, etc. */
void AFlatbufferTestActor::MyAdvancedFlatBufferTest()
{
    // We'll do a second buffer to show advanced concepts
    flatbuffers::FlatBufferBuilder builder;

    auto nameOffset = builder.CreateString("UnrealMonster_Advanced");
    std::vector<uint8_t> inventoryData{ 100, 101, 102, 103 };
    auto inventoryOffset = builder.CreateVector(inventoryData);

    // Create the root
    auto fbOffset = CreateFCheckFlatBuffer(
        builder,
        /*mana=*/999,
        /*hp=*/888,
        nameOffset,
        inventoryOffset,
        ECharacterType_AI
    );

    FinishFCheckFlatBufferBuffer(builder, fbOffset);

    const uint8_t* bufferPtr = builder.GetBufferPointer();
    const size_t   bufferSz = builder.GetSize();

    flatbuffers::Verifier v(bufferPtr, bufferSz);
    if (!VerifyFCheckFlatBufferBuffer(v))
    {
        UE_LOG(LogTemp, Error, TEXT("[MyAdvancedFlatBufferTest] Verification failed!"));
        return;
    }

    auto root = GetFCheckFlatBuffer(bufferPtr);
    if (!root)
    {
        UE_LOG(LogTemp, Error, TEXT("[MyAdvancedFlatBufferTest] No root found!"));
        return;
    }

    UE_LOG(LogTemp, Display, TEXT("=== MyAdvancedFlatBufferTest ==="));
    UE_LOG(LogTemp, Display, TEXT("Name: %s"), UTF8_TO_TCHAR(root->name()->c_str()));
    UE_LOG(LogTemp, Display, TEXT("Mana: %d"), root->mana());
    UE_LOG(LogTemp, Display, TEXT("HP:   %d"), root->hp());
    UE_LOG(LogTemp, Display, TEXT("Type: %d"), (int32)root->chtype());

    // For demonstration, let's mutate the HP to 1234 in place:
    auto mutRoot = GetMutableFCheckFlatBuffer((void*)bufferPtr);
    if (mutRoot)
    {
        bool successHP = mutRoot->mutate_hp(1234);
        UE_LOG(LogTemp, Display, TEXT("In-place HP mutation success? %d"), successHP);
    }

    // (Optional) Using the Object API if you compiled with --gen-object-api:
    // (Requires you to have "native_type" or just normal generation with object API)
    // 
    //    auto obj = UnPackFCheckFlatBuffer(bufferPtr);
    //    if (obj)
    //    {
    //        UE_LOG(LogTemp, Display, TEXT("Object API => Name: %s"), *FString(obj->name.c_str()));
    //        obj->mana = 5555;
    //        // re-pack if needed
    //        flatbuffers::FlatBufferBuilder builder2;
    //        auto newNameOffset = builder2.CreateString(obj->name);
    //        auto newInventoryOffset = builder2.CreateVector(obj->inventory);
    //        auto newFBOffset = CreateFCheckFlatBuffer(builder2, obj->mana, obj->hp, newNameOffset, newInventoryOffset, obj->chtype);
    //        FinishFCheckFlatBufferBuffer(builder2, newFBOffset);
    //        // Now you have a new buffer with updated data
    //    }
}

/** Helper to demonstrate in-place mutation on 'FCheckFlatBuffer' data */
void AFlatbufferTestActor::MutateFCheckBufferTest(uint8_t* flatbuf, size_t length)
{
    auto root = GetMutableFCheckFlatBuffer(flatbuf);
    if (!root)
    {
        UE_LOG(LogTemp, Error, TEXT("MutateFCheckBufferTest: Root is NULL"));
        return;
    }

    // Attempt to mutate fields
    bool manaOK = root->mutate_mana(123);
    bool hpOK = root->mutate_hp(456);
    UE_LOG(LogTemp, Display, TEXT("MutateFCheckBufferTest => manaOK:%d, hpOK:%d"), manaOK, hpOK);

    // Try changing some inventory element if it exists
    auto inv = root->mutable_inventory();
    if (inv && inv->size() > 1)
    {
        inv->Mutate(1, 99);
        UE_LOG(LogTemp, Display, TEXT("Mutated Inventory[1] => 99"));
    }

    // Re-verify
    flatbuffers::Verifier v(flatbuf, length);
    if (!VerifyFCheckFlatBufferBuffer(v))
    {
        UE_LOG(LogTemp, Error, TEXT("After in-place mutation, buffer is invalid!"));
        return;
    }

    UE_LOG(LogTemp, Display, TEXT("In-place mutation completed successfully."));
}
