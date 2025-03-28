// Fill out your copyright notice in the Description page of Project Settings.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "FlatbufferTestActor.generated.h"


UENUM(BlueprintType)
enum class ECharacterType : uint8
{
	UseDefault,
	Player,
	AI,
	Hidden
};

USTRUCT(BlueprintType, Category = FlatBuffer)
struct FCheckFlatBuffer
{
	GENERATED_BODY()

    FVector Pos;

    UPROPERTY(meta=(FlatBufferDefault="150"))
    int32 Mana = 150;

    UPROPERTY(meta=(FlatBufferDefault="100"))
    int32 HP = 100;

    UPROPERTY()
    FString Name;

    UPROPERTY()
    TArray<uint8> Inventory;

	UPROPERTY()
	ECharacterType ChType = ECharacterType::Hidden;

    FString Color;
};

UCLASS()
class TESTINGFLATBUFFERS_API AFlatbufferTestActor : public AActor
{
	GENERATED_BODY()
	
public:
    AFlatbufferTestActor();

protected:
    virtual void BeginPlay() override;
    virtual void Tick(float DeltaTime) override;

private:
    // Demonstrates building, verifying, and reading "FCheckFlatBuffer"
    void MyBasicFlatBufferTest();

    // Demonstrates advanced usage (mutation, object API, etc.)
    void MyAdvancedFlatBufferTest();

    // Helper function for in-place mutation
    void MutateFCheckBufferTest(uint8_t* flatbuf, size_t length);
};