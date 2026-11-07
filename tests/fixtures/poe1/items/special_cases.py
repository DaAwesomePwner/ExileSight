"""Additional deliberately small synthetic edge cases with manually specified expectations."""
from generate import save

def case(cls,slug,rarity,name,base,body,advanced,expected,p=None,a=None):
 head=f'Item Class: {cls}\nRarity: {rarity}\n{name}'+ ('\n'+base if base!=name else '')+'\n--------\n'
 save(cls,slug,head+body,head+advanced,dict(ItemClass=cls,Name=name,**expected),p,a)
case('Amulets','anointed','Rare','Doom Beads','Onyx Amulet',
 'Allocates Charisma (enchant)\n--------\n+16 to all Attributes (implicit)\n--------\n+92 to maximum Life (fractured)\n--------\nNote: ~price 3/2 divine',
 '{ Enchantment Modifier }\nAllocates Charisma (enchant)\n--------\n{ Implicit Modifier }\n+16(10-16) to all Attributes (implicit)\n--------\n{ Fractured Prefix Modifier "Healthy" (Tier: 1) — Life }\n+92(90-99) to maximum Life (fractured)\n--------\nNote: ~price 3/2 divine',
 dict(Anointment='Charisma',Fractured=True,Price=dict(Amount=1.5,Currency='divine'),EnchantMods=[dict(Kind='Enchant')],ImplicitMods=[dict(Kind='Implicit')],FracturedMods=[dict(Kind='Explicit, Fractured')]))
case('Body Armours','tabula','Unique','Tabula Rasa','Simple Robe',
 'Sockets: W-W-W-W-W-W\n--------\nItem Level: 80',
 'Sockets: W-W-W-W-W-W\n--------\nItem Level: 80',dict(Rarity='Unique',SocketCount=6,MaximumLinks=6,Mods=[]))
case('Rings','kaoms-sign','Unique',"Kaom's Sign",'Coral Ring',
 '+30 to maximum Life (implicit)\n--------\n+15 to Strength\n+1 to Maximum Endurance Charges\n--------\nA token from the sea.\nA promise from the king.',
 '{ Implicit Modifier }\n+30(20-30) to maximum Life (implicit)\n--------\n{ Unique Modifier }\n+15(10-20) to Strength\n{ Unique Modifier }\n+1 to Maximum Endurance Charges\n--------\nA token from the sea.\nA promise from the king.',dict(FlavourText=['A token from the sea.','A promise from the king.']))
case('One Hand Swords','weapon-properties','Rare','Doom Edge','Jewelled Foil',
 'Physical Damage: 100-200 (augmented)\nElemental Damage: 12-24, 30-60 (augmented)\nCritical Strike Chance: 6.50%\nAttacks per Second: 1.80\n--------\nRequirements:\nLevel: 68\nDex: 212\n--------\nItem Level: 85',
 'Physical Damage: 100-200 (augmented)\nElemental Damage: 12-24, 30-60 (augmented)\nCritical Strike Chance: 6.50%\nAttacks per Second: 1.80\n--------\nRequirements:\nLevel: 68\nDex: 212\n--------\nItem Level: 85',
 dict(Requirements=dict(Level=68,Dex=212),Properties={'Physical Damage':dict(Values=[dict(Value=100),dict(Value=200)]),'Attacks per Second':dict(Values=[dict(Value=1.8)])}))
case('Helmets','eldritch','Rare','Doom Crown','Hubris Circlet',
 'Energy Shield: 200\n--------\n+12% to Fire Resistance (implicit)\n+12% to Cold Resistance (implicit)',
 'Energy Shield: 200\n--------\n{ Searing Exarch Implicit Modifier (Tier: 3) }\n+12(10-12)% to Fire Resistance (implicit)\n{ Eater of Worlds Implicit Modifier (Tier: 3) }\n+12(10-12)% to Cold Resistance (implicit)',dict(Properties={'Energy Shield':dict(Values=[dict(Value=200)])}),
 dict(Influences=[]),dict(Influences=['Searing Exarch','Eater of Worlds']))
case('Expedition Logbooks','modded-logbook','Rare','Doom Expedition','Expedition Logbook',
 'Item Level: 83\n--------\n20% increased number of Remnants\n30% increased Explosive Radius',
 'Item Level: 83\n--------\n{ Prefix Modifier "Expansive" (Tier: 1) }\n20(15-20)% increased number of Remnants\n30(25-30)% increased Explosive Radius',dict(ItemLevel=83),dict(Mods=[dict(Text='20% increased number of Remnants'),dict(Text='30% increased Explosive Radius')]),dict(Mods=[dict(Group=1,Tier=1),dict(Group=1,Tier=1)]))
