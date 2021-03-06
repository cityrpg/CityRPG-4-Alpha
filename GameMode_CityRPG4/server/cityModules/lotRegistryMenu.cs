// ============================================================
// CityRPG 4 Lot Registry Menu Functions
// ============================================================

function GameConnection::cityLotIndexClear(%client)
{
	for(%i = 0; %i <= %client.cityLotIndexCount; %i++)
	{
		%client.cityLotIndex[%i] = 0;
		%client.cityLotIndexCount = 0;
	}
}

// Brick.cityLotDisplayRefresh()
// Re-displays the lot's info to all players currently on the lot.
function fxDTSBrick::cityLotDisplayRefresh(%lotBrick)
{
	for(%i = 0; %i <= getFieldCount(%lotBrick.lotOccupants)-1; %i++)
	{
		%targetClient = getField(%lotBrick.lotOccupants, %i);

		%targetClient.cityLotDisplay(%lotBrick);
	}
}

function CityMenu_Lot(%client, %input)
{
	if(%client.cityMenuBack !$= "")
	{
		// "Go back" support for sub-menus
		%lotBrick = %client.cityMenuBack;
		%client.cityMenuBack = "";
	}
	else if(%input !$= "")
	{
		// If not going back and there's input, we're picking a lot from one of the real estate menus. Match it accordingly.
		%lotID = %client.cityLotIndex[%input];
		%lotBrick = findLotBrickByID(%lotID);

		// Indicate that we're a sub-menu so we can display "Back" instead of "Close" later.
		// cityMenuBack identifies the real estate office by its brick.
		%isSubMenu = 1;
		%client.cityMenuBack = %client.cityMenuID;

		%client.cityLotIndexClear();
	}
	else
	{
		// No input, we're running via /lot.
		if(%client.CityLotBrick $= "")
		{
			%client.cityMenuMessage("\c6You are currently not on a lot.");
			return;
		}

		%lotBrick = %client.CityLotBrick;
	}

	if(!isObject(%lotBrick) || %lotBrick.getDataBlock().CityRPGBrickType != $CityBrick_Lot)
	{
		error("Lot Menu - Attempting to access invalid lot '" @ %lotBrick @ "'! Something is seriously wrong.");
		return;
	}

	// ## Initial display ## //
	%price = %lotBrick.dataBlock.initialPrice;

	if(%lotBrick.getCityLotID() == -1)
	{
		error("Attempting to access a blank lot on brick '" @ %lotBrick @ "'! Re-initializing it...");

		%lotBrick.initNewCityLot();
	}

	%title = "\c3" @ %lotBrick.getCityLotName() @ "\c6 - " @ %lotBrick.getDataBlock().uiName;

	if(%lotBrick.getCityLotPreownedPrice() != -1)
	{
		%client.cityMenuMessage("\c6This lot is listed for sale by owner for \c2$" @ %lotBrick.getCityLotPreownedPrice() @ "\c6.");
	}

	// ## Options for all lots ## //
	%menu = "View lot rules.";
			//TAB "View warning log."

	%functions =	"CityMenu_LotRules";
						//TAB "CityMenu_Placeholder"

	// ## Options for unclaimed lots ## //
	if(%lotBrick.getCityLotOwnerID() == -1)
	{
		%client.cityMenuMessage("\c6This lot is for sale! It can be purchased for \c2$" @ %price @ "\c6.");

		// Place these options first.
		%menu = "Purchase this lot. " TAB %menu;
		%functions = "CityMenu_LotPurchasePrompt" TAB %functions;
	}

	// ## Options for lot owners ## //
	if(%lotBrick.getCityLotOwnerID() == %client.bl_id)
	{
		%menu = %menu TAB "Lot management.";
		%functions = %functions TAB "CityMenu_LotOwnerManagement";
	}

	// ## Options for non-owners only ## //
	else if(%lotBrick.getCityLotPreownedPrice() != -1)
	{
		%menu = %menu TAB "Purchase this lot.";
		%functions = %functions TAB "CityMenu_Lot_PurchasePreownedPrompt";
	}

	// ## Options for admins ## //
	if(%client.isAdmin)
	{
		%menu = %menu TAB "\c4Open admin menu.";
		%functions = %functions TAB "CityMenu_LotAdmin";
	}

	// ## Finalization ## //
	if(%isSubMenu)
	{
		%menu = %menu TAB "Go back.";
		%functions = %functions TAB "CityMenu_RealEstate";
	}
	else
	{
		%menu = %menu TAB "Close menu.";
		%functions = %functions TAB "CityMenu_Close";
	}

	// Use the lot brick as the menu ID
	%client.cityMenuOpen(%menu, %functions, %lotBrick, "\c3Lot menu closed.", 0, 1, %title);
}

// ## Functions for all lots ## //
function CityMenu_LotRules(%client)
{
	%client.cityMenuMessage("\c3Code enforcement requires following restrictions on this lot:");

	%lotRules = $Pref::Server::City::LotRules;
	%client.cityMenuMessage("\c6" @ %lotRules);
}

// ## Functions for unclaimed lots ## //
function CityMenu_LotPurchasePrompt(%client)
{
	%lotBrick = %client.cityMenuID;

	%client.cityLog("Lot " @ %lotBrick.getCityLotID() @ " purchase prompt");

	if(City.get(%client.bl_id, "money") >= %lotBrick.dataBlock.initialPrice)
	{
		%client.cityMenuMessage("\c6You are purchasing this lot for \c2$" @ %lotBrick.dataBlock.initialPrice @ "\c6. Make sure you have read the lot rules. Lot sales are final!");
		%client.cityMenuMessage("\c6Type \c31\c6 to confirm, or leave the lot to cancel.");

		%client.cityMenuFunction = CityLots_PurchaseLot;
		%client.cityMenuID = %lotBrick;
	}
	else
	{
		%client.cityMenuMessage("\c6You need \c3$" @ %lotBrick.dataBlock.initialPrice @ "\c6 on hand to purchase this lot.");
		%client.cityMenuClose();
	}
}

function CityLots_PurchaseLot(%client, %input, %lotBrick)
{
	if(%lotBrick $= "")
	{
		%lotBrick = %client.cityMenuID;
	}

	%buyerCash = City.get(%client.bl_id, "money");

	if(%input !$= "1")
	{
		%client.cityMenuMessage("\c0Lot purchase cancelled.");
		%client.cityMenuClose();
	}
	else if(%lotBrick.getCityLotOwnerID() != -1 || %buyerCash < %lotBrick.dataBlock.initialPrice)
	{
		%client.cityLog("Lot " @ %lotBrick.getCityLotID() @ " purchase fell through", 0, 1);

		// Security check falls through
		%client.cityMenuMessage("\c0Sorry, you are no-longer able to purchase this lot at this time.");
		%client.cityMenuClose();
	}
	else if(%buyerCash >= %lotBrick.dataBlock.initialPrice)
	{
		%client.cityLog("Lot " @ %lotBrick.getCityLotID() @ " purchase success");

		City.subtract(%client.bl_id, "money", %lotBrick.dataBlock.initialPrice);
		%client.cityMenuMessage("\c6You have purchased this lot for \c2$" @ %lotBrick.dataBlock.initialPrice @ "\c6!");

		%client.setInfo();

		CityLots_TransferLot(%client.cityMenuID, %client.bl_id); // The menu ID is the lot brick ID
		%client.cityMenuID.setCityLotTransferDate(getDateTime());

		%lotBrick.cityLotDisplayRefresh();

		// Open the menu for the new lot
		CityMenu_Lot(%client);
	}
}

// ## Functions for lot owners ## //
function CityMenu_LotOwnerManagement(%client)
{
	%lotBrick = %client.cityMenuID;
	%client.cityMenuClose(true);
	%ownerID = %lotBrick.getCityLotOwnerID();
	%client.cityMenuBack = %lotBrick;

	%menu = "Rename lot."
			TAB "Wrench lot.";

	%functions = "CityMenu_LotSetNamePrompt"
			 TAB "CityMenu_LotWrench";

	if(%lotBrick.getCityLotPreownedPrice() == -1)
	{
		%menu = %menu TAB "List this lot for sale.";
		%functions = %functions TAB "CityMenu_Lot_ListForSalePrompt";
	}
	else
	{
		%menu = %menu TAB "Take this lot off sale.";
		%functions = %functions TAB "CityMenu_Lot_RemoveFromSale";
	}

	%menu = %menu TAB "Go back.";
	%functions = %functions TAB "CityMenu_Lot";

	%client.cityMenuOpen(%menu, %functions, %lotBrick, "\c3Lot menu closed.", 0, 1);
}

function CityMenu_LotSetNamePrompt(%client)
{
	%client.cityLog("Lot " @ %client.cityMenuID.getCityLotID() @ " rename prompt");

	%client.cityMenuMessage("\c6Enter a new name for your lot.");
	%client.cityMenuFunction = CityMenu_LotSetName;
}

function CityMenu_LotSetName(%client, %input)
{
	%lotBrick = %client.cityMenuID;

	%client.cityLog("Lot " @ %lotBrick.getCityLotID() @ " rename '" @ %input @ "'");

	if(%lotBrick.getCityLotOwnerID() != %client.bl_id)
	{
		return;
	}

	if(strlen(%input) > 40)
	{
		%client.cityMenuMessage("\c6Sorry, that name exceeds the length limit. Please try again.");
		return;
	}

	%name = StripMLControlChars(%input);

	%lotBrick.setCityLotName(%name);
	%client.cityMenuMessage("\c6Lot name changed to \c3" @ %lotBrick.getCityLotName() @ "\c6.");
	%lotBrick.cityLotDisplayRefresh();

	%client.cityMenuClose();
}

function CityMenu_LotWrench(%client)
{
	// Set the hit obj to the lot brick
	%hitObj = %client.cityMenuID;

	// Close the menu -- we're pivoting to a built-in game menu.
	%client.cityMenuClose();

	// Wacky hacky fun time! We're directly calling WrenchImage.onHitObject to open the dialog as if the player wrenched the brick.
	WrenchImage.onHitObject(%client.player, 2, %hitObj, %client.player.position, %client.player.getEyePoint());
}

function CityMenu_Lot_RemoveFromSale(%client)
{
	%lotBrick = %client.cityMenuID;

	%client.cityLog("Lot " @ %lotBrick.getCityLotID() @ " remove from sale");

	// This will remove it from CitySO.lotListings as well.
	%lotBrick.setCityLotPreownedPrice(-1);
	%lotBrick.cityLotDisplayRefresh();

	%client.cityMenuMessage("\c6You have taken this lot off sale.");
	%client.cityMenuClose();
}

// ### Listing for sale ### //
function CityMenu_Lot_ListForSalePrompt(%client, %input)
{
	%lotBrick = %client.cityMenuID;

	%client.cityLog("Lot " @ %lotBrick.getCityLotID() @ " list for sale prompt");

	%client.cityMenuMessage("\c6Listing this lot for sale will allow someone to buy it for the price of your choosing.");
	%client.cityMenuMessage("\c6How much money would you like to sell this lot for? Enter a number, or leave to cancel.");

	%client.cityMenuFunction = CityMenu_Lot_ListForSaleConfirmPrompt;
}

function CityMenu_Lot_ListForSaleConfirmPrompt(%client, %input)
{
	%price = atof(%input);
	%lotBrick = %client.cityMenuID;

	if(%price < 0)
		%price = 0;

	%client.cityMenuMessage("\c6You are listing the lot \c3" @ %lotBrick.getCityLotName() @ "\c6 on sale for \c2$" @ strFormatNumber(%price));
	%client.cityMenuMessage("\c0Warning!\c6 Once someone purchases this lot, they will become the permanent owner of your lot. Are you sure?");

	%client.cityLotPrice = %price;

	if(%price == 0)
		%client.cityMenuMessage("\c0You are about to list this lot for free. Are you sure?");

	%client.cityMenuMessage("\c6Type \c31\c6 to confirm, or \c32\c6 to cancel.");

	%client.cityMenuFunction = CityMenu_Lot_ListForSale;
}

function CityMenu_Lot_ListForSale(%client, %input)
{
	%lotBrick = %client.cityMenuID;
	%lotID = %lotBrick.getCityLotID();

	if(%input !$= "1")
	{
		%client.cityMenuMessage("\c0Lot listing cancelled.");
		%client.cityMenuClose();
		return;
	}

	// Security check
	if(%lotBrick.getCityLotOwnerID() != %client.bl_id)
	{
		%client.cityLog("Lot " @ %lotBrick.getCityLotID() @ " sale listing fell through", 0, 1);

		// Security check falls through
		%client.cityMenuMessage("\c0Sorry, you are no-longer able to list that lot for sale at this time.");
		%client.cityMenuClose();
		return;
	}

	%client.cityLog("Lot " @ %lotID @ " listing success");

	// This will append the lot to the fields under CitySO.lotListings.
	%lotBrick.setCityLotPreownedPrice(%client.cityLotPrice);
	%lotBrick.cityLotDisplayRefresh();

	%client.cityMenuMessage("\c6You have listed your lot for sale.");
	%client.cityMenuClose();
}

// ## Functions for on-sale lots ## //
function CityMenu_Lot_PurchasePreownedPrompt(%client)
{
	%lotBrick = %client.cityMenuID;

	%client.cityLog("Lot " @ %lotBrick.getCityLotID() @ " pre-owned purchase prompt");

	if(City.get(%client.bl_id, "money") >= %lotBrick.getCityLotPreownedPrice())
	{
		%client.cityMenuMessage("\c6You are purchasing this lot from \c3" @ %lotBrick.getGroup().name @ "\c6 for \c2$" @ %lotBrick.getCityLotPreownedPrice() @ "\c6. Make sure you have read the lot rules. Lot sales are final!");
		%client.cityMenuMessage("\c6Type \c31\c6 to confirm, or leave the lot to cancel.");

		%client.cityMenuFunction = CityMenu_Lot_PurchasePreowned;
		%client.cityMenuID = %lotBrick;

		// Lock in the purchase details -- this is necessary in case they change mid-purchase
		%client.cityLotPurchasePrice = %lotBrick.getCityLotPreownedPrice();
		%client.cityLotPurchaseOwner = %lotBrick.getCityLotOwnerID();
	}
	else
	{
		%client.cityMenuMessage("\c6You need \c3$" @ %lotBrick.getCityLotPreownedPrice() @ "\c6 on hand to purchase this lot.");
		%client.cityMenuClose();
	}
}

function CityMenu_Lot_PurchasePreowned(%client, %input, %lotBrick)
{
	if(%lotBrick $= "")
	{
		%lotBrick = %client.cityMenuID;
	}

	%lotOwner = %client.cityLotPurchaseOwner;
	%lotPrice = %client.cityLotPurchasePrice;

	%buyerCash = City.get(%client.bl_id, "money");

	if(%input !$= "1")
	{
		%client.cityMenuMessage("\c0Lot purchase cancelled.");
		%client.cityMenuClose();
	}
	else if(%lotOwner != %lotBrick.getCityLotOwnerID() || %lotPrice != %lotBrick.getCityLotPreownedPrice() || %buyerCash < %lotPrice)
	{
		%client.cityLog("Lot " @ %lotBrick.getCityLotID() @ " pre-owned purchase fell through", 0, 1);

		// Security check falls through
		%client.cityMenuMessage("\c0Sorry, you are no-longer able to purchase this lot at this time.");
		%client.cityMenuClose();
	}
	else if(%buyerCash >= %lotPrice)
	{
		%client.cityLog("Lot " @ %lotBrick.getCityLotID() @ " pre-owned purchase success");

		// Transfer the money between buyer and owner.
		City.subtract(%client.bl_id, "money", %lotPrice);
		City.add(%lotOwner, "bank", %lotPrice);

		%ownerClient = findClientByBL_ID(%lotOwner);
		if(%ownerClient != 0)
		{
			messageClient(%ownerClient, '', "\c6Your lot, \c3" @ %lotBrick.getCityLotName() @ "\c6, has been purchased by \c3" @ %client.name @ "\c6 for \c2$" @ %lotPrice @ "\c6. The money has been deposited into your bank.");
		}

		%client.cityMenuMessage("\c6You have purchased this lot from \c3" @ %lotBrick.getGroup().name @ "\c6 for \c2$" @ %lotPrice @ "\c6.");

		%client.setInfo();

		// This transfer will automatically reset the state of the lot as 'not for sale'.
		CityLots_TransferLot(%client.cityMenuID, %client.bl_id); // The menu ID is the lot brick ID
		%client.cityMenuID.setCityLotTransferDate(getDateTime());
		%lotBrick.cityLotDisplayRefresh();

		// Open the menu for the new lot
		CityMenu_Lot(%client);
	}
}

// ## Functions for admins ## //
function CityMenu_LotAdmin(%client)
{
	%lotBrick = %client.CityMenuID;
	%client.cityMenuClose(true);
	%ownerID = %lotBrick.getCityLotOwnerID();

	%client.cityMenuBack = %lotBrick;

	%client.cityMenuMessage("\c3Lot Admin\c6 for: \c3" @ %lotBrick.getCityLotName() @ "\c6 - Lot ID: \c3" @ %lotBrick.getCityLotID() @ "\c6 - Brick ID: \c3" @ %lotBrick.getID() @ "\c6 - Lot purchase date: \c3" @ %lotBrick.getCityLotTransferDate());

	if(%ownerID != -1)
	{
		%client.cityMenuMessage("\c6Owner: \c3" @ City.get(%ownerID, "name") @ "\c6 (ID \c3" @ %lotBrick.getCityLotOwnerID() @ "\c6)");
	}
	else
	{
		%client.cityMenuMessage("\c6Lot is owned by the city.");
	}
	
	%menu = "Force rename."
			TAB "Transfer lot to the city."
			TAB "Transfer lot to a player."
			TAB "Link lot."
			TAB "Wrench lot."
			TAB "Go back.";

	%functions =	"CityMenu_LotAdmin_SetNamePrompt"
						TAB "CityMenu_LotAdmin_TransferCity"
						TAB "CityMenu_LotAdmin_TransferPlayerPrompt"
						TAB "CityMenu_LotAdmin_LinkPrompt"
						TAB "CityMenu_LotWrench"
						TAB "CityMenu_Lot";

	%client.cityMenuOpen(%menu, %functions, %lotBrick, "\c3Lot menu closed.", 0, 1);
}

function CityMenu_LotAdmin_SetNamePrompt(%client)
{
	%client.cityLog("Lot MOD " @ %client.cityMenuID.getCityLotID() @ " rename prompt");
	%client.cityMenuMessage("\c6Enter a new name for the lot \c3" @ %client.cityMenuID.getCityLotName() @ "\c6. ML tags are allowed.");
	%client.cityMenuFunction = CityMenu_LotAdmin_SetName;
}

function CityMenu_LotAdmin_SetName(%client, %input)
{
	%lotBrick = %client.cityMenuID;
	%client.cityLog("Lot MOD " @ %lotBrick.getCityLotID() @ " rename '" @ %input @ "'");

	if(strlen(%input) > 40)
	{
		%client.cityMenuMessage("\c6Sorry, that name exceeds the length limit. Please try again.");
		return;
	}

	%lotBrick.setCityLotName(%input);
	%client.cityMenuMessage("\c6Lot name changed to \c3" @ %client.cityMenuID.getCityLotName() @ "\c6.");
	%lotBrick.cityLotDisplayRefresh();

	%client.cityMenuClose();
}

function CityMenu_LotAdmin_TransferCity(%client)
{
	%hostID = getNumKeyID();

	%lotBrick = %client.cityMenuID;

	%client.cityLog("Lot MOD " @ %lotBrick.getCityLotID() @ " transfer city");

	CityLots_TransferLot(%lotBrick, %hostID);
	%lotBrick.setCityLotTransferDate(getDateTime());

	%lotBrick.setCityLotName("Unclaimed Lot");
	%lotBrick.setCityLotOwnerID(-1);
	%lotBrick.cityLotDisplayRefresh();

	%client.cityMenuMessage("\c6Lot transferred to the city successfully.");
	%client.cityMenuClose();
}

function CityMenu_LotAdmin_TransferPlayerPrompt(%client)
{
	%client.cityLog("Lot MOD " @ %client.cityMenuID.getCityLotID() @ " transfer pl prompt");

	%client.cityMenuMessage("\c6Enter a Blockland ID of the player to transfer the lot to.");
	%client.cityMenuFunction = CityMenu_LotAdmin_TransferPlayer;
}

function CityMenu_LotAdmin_TransferPlayer(%client, %input)
{
	%lotBrick = %client.cityMenuID;
	%client.cityLog("Lot MOD " @ %lotBrick.getCityLotID() @ " transfer pl '" @ %input @ "'");

	%target = findClientByBL_ID(%input);

	// Hacky workaround to detect if a non-number is passed to avoid pain.
	if(%input == 0 && %input !$= "0")
	{
		%client.cityMenuMessage("\c3" @ %input @ "\c6 is not a valid Blockland ID. Please try again.");
		return;
	}

	CityLots_TransferLot(%client.cityMenuID, %input);
	%lotBrick.setCityLotTransferDate(getDateTime());
	%lotBrick.cityLotDisplayRefresh();

	%client.cityMenuClose();
}

function CityMenu_LotAdmin_LinkPrompt(%client)
{
	%lotBrick =  %client.cityMenuID;
	%client.cityLog("Lot MOD " @ %lotBrick.getCityLotID() @ " link prompt");

	%client.cityMenuMessage("\c6This lot \c3" @ %lotBrick.getCityLotID() @ "\c6 will become the base lot.");
	%client.cityMenuMessage("\c6Enter the lot ID you would like to link. This number can be found in the admin menu of the target lot, under \"Lot ID\".");
	%client.cityMenuFunction = CityMenu_LotAdmin_LinkPromptConfirm;
}

function CityMenu_LotAdmin_LinkPromptConfirm(%client, %input)
{
	%brickLinkA = %client.cityMenuID;
	%brickLinkB = findLotBrickByID(atof(%input));

	if(%brickLinkB == 0)
	{
		%client.cityMenuMessage("\c0Unable to find the target specified.");
		%client.cityMenuClose();
		return;
	}

	if(%brickLinkA == %brickLinkB)
	{
		%client.cityMenuMessage("\c0You attempt to link a lot to itself, creating a singularity that vaporizes the entire city. Nice one.");
		%client.cityMenuClose();
		return;
	}

	if(%brickLinkA.getCityLotIsBase() && %brickLinkB.getCityLotIsBase())
	{
		%client.cityMenuMessage("\c0You attempt to link a base lot to another base lot, creating a singularity that vaporizes the entire city. Hail Lord Singuloth.");
		%client.cityMenuClose();
		return;
	}

	// Base-> Temp
	// Temp -> Base
	// TODO: Temp -> Temp? (Should funnel to base)
	if(%brickLinkB.getCityLotIsBase())
	{
		// If the target is a base, reverse the order.
		%brickLinkBase = %brickLinkB;
		%brickLinkTarget = %brickLinkA;
		%client.cityMenuMessage("\c6The lot you inputted is already a base brick. The lot you are currently on will become linked to this existing base.");
	}
	else
	{
		%brickLinkBase = %brickLinkA;
		%brickLinkTarget = %brickLinkB;
		%client.cityMenuMessage("\c6The lot you are currently on will become the \c3base brick\c6.");
	}

	if(%brickLinkBase.getCityLotOwnerID() != %brickLinkTarget.getCityLotOwnerID())
	{
		%client.cityMenuMessage("\c6These two lots have different owners. The target lot will be transferred to the owner of the base lot.");
	}

	%client.brickLinkBase = %brickLinkBase;
	%client.brickLinkTarget = %brickLinkTarget;

	// We will need to record the lot owners for additional validation.
	%client.brickLinkBaseOwner = %brickLinkBase.getCityLotOwnerID();
	%client.brickLinkTargetOwner = %brickLinkTarget.getCityLotOwnerID();

	%client.cityMenuMessage("\c6You are \c0permanently linking\c6 the base lot \c3" @ %brickLinkBase.getCityLotID() @ "\c6 to the target lot \c3" @ %brickLinkTarget.getCityLotID() @ "\c6.");
	%client.cityMenuMessage("\c6Any data on the target lot will be destroyed. The only way to undo this will be to destroy the base and linked bricks and start over.");

	%client.cityMenuMessage("\c6Type \c31\c6 to confirm, or leave the lot to cancel.");
	%client.cityMenuFunction = CityMenu_LotAdmin_Link;
}

function CityMenu_LotAdmin_Link(%client)
{
	%brickLinkBase = %client.brickLinkBase;
	%brickLinkTarget = %client.brickLinkTarget;

	// Ensure that ownership has not changed in between prompts to avoid really bad things.
	if(%client.brickLinkBaseOwner != %brickLinkBase.getCityLotOwnerID() || %client.brickLinkTargetOwner != %brickLinkTarget.getCityLotOwnerID())
	{
		%client.cityLog("Lot link " @ %brickLinkBase.getCityLotID() SPC %brickLinkTarget.getCityLotID() @ " fell through", 0, 1);
		%client.cityMenuMessage("\c0Link aborted due to lot ownership change. You may try linking the lots again.");
		%client.cityMenuClose();
		return;
	}

	%client.cityLog("Lot MOD link " @ %brickLinkBase.getCityLotID() @ " to " @ %brickLinkTarget.getCityLotID());
	%brickLinkBase.linkCityLot(%brickLinkTarget);
}
