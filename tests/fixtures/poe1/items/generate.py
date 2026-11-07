"""Reproducible synthetic inputs and independent expected assertions; never reads parser output."""
import json
from pathlib import Path
root=Path(__file__).parent
bases=[('Body Armours','Glorious Plate'),('Helmets','Royal Burgonet'),('Gloves','Titan Gauntlets'),('Boots','Titan Greaves'),('Shields','Pinnacle Tower Shield'),('Quivers','Spike-Point Arrow Quiver'),('Rings','Gold Ring'),('Amulets','Onyx Amulet'),('Belts','Leather Belt'),('One Hand Swords','Jewelled Foil'),('Two Hand Swords','Exquisite Blade'),('One Hand Axes','Siege Axe'),('Two Hand Axes','Vaal Axe'),('One Hand Maces','Behemoth Mace'),('Two Hand Maces','Karui Maul'),('Daggers','Platinum Kris'),('Rune Daggers','Imperial Skean'),('Claws','Imperial Claw'),('Wands','Imbued Wand'),('Sceptres','Void Sceptre'),('Staves','Judgement Staff'),('Warstaves','Ezomyte Staff'),('Bows','Spine Bow'),('Life Flasks','Divine Life Flask'),('Mana Flasks','Eternal Mana Flask'),('Hybrid Flasks','Hallowed Hybrid Flask'),('Utility Flasks','Granite Flask'),('Jewels','Cobalt Jewel'),('Jewels','Large Cluster Jewel'),('Maps','Dunes Map')]
def save(cls,slug,plain,advanced,common,p=None,a=None):
 d=root/cls.lower().replace(' ','-');d.mkdir(exist_ok=True)
 for kind,txt in [('plain',plain),('advanced',advanced)]: (d/(slug+'.'+kind+'.txt')).write_text(txt+'\n')
 (d/(slug+'.expected.json')).write_text(json.dumps(dict(common=common,plain=p or {},advanced=a or {}),indent=2)+'\n')
for i,(cls,base) in enumerate(bases):
 for j in range(2):
  rarity=['Normal','Magic','Rare','Rare'][(i*2+j)%4]
  if 'Flasks' in cls: rarity='Magic' if j else 'Normal'
  name={'Normal':base,'Magic':'Stalwart '+base,'Rare':'Doom Shelter','Unique':'Relic of the Dawn'}[rarity]
  header=f'Item Class: {cls}\nRarity: {rarity}\n{name}'+ ('\n'+base if rarity in ['Rare','Unique'] else '')
  props='Item Level: 84';common=dict(Game='PoE1',ItemClass=cls,Rarity=rarity,Name=name,BaseType=None if rarity=='Magic' else base,ItemLevel=84)
  if cls=='Maps': props+='\nMap Tier: 16\nItem Quantity: +80% (augmented)\nItem Rarity: +40% (augmented)\nMonster Pack Size: +25% (augmented)';common.update(MapTier=16,MapQuantity=80,MapRarity=40,MapPackSize=25)
  if cls in ['Body Armours','Helmets','Gloves','Boots','Shields']: props+='\nArmour: 400 (augmented)\nQuality: +20% (augmented)\nSockets: R-R-G B';common.update(Quality=20,SocketCount=4,MaximumLinks=3)
  flags=['Corrupted','Mirrored','Unidentified','Fractured Item','Synthesised Item','Split','Shaper Item','Elder Item','Crusader Item','Hunter Item','Redeemer Item','Warlord Item','Searing Exarch Item','Eater of Worlds Item']
  flag=flags[i%len(flags)] if j and 'Flasks' not in cls else ''
  tail=('\n--------\n'+flag) if flag else ''
  if flag.endswith(' Item') and flag not in ['Fractured Item','Synthesised Item']: common['Influences']=[flag[:-5]]
  elif flag: common[flag.replace(' Item','')]=True
  if flag=='Unidentified':
   name=base;header=f'Item Class: {cls}\nRarity: {rarity}\n{base}';common.update(Name=base,BaseType=base)
  mod='';adv='';mp=[];ma=[]
  if rarity!='Normal' and flag!='Unidentified':
   text='Monsters deal 25% extra Physical Damage as Fire' if cls=='Maps' else '+92 to maximum Life'
   rolled=text if cls=='Maps' else '+92(90-99) to maximum Life'
   if i in range(9,23):
    text='120% increased Physical Damage';rolled='120(110-129)% increased Physical Damage'
   if 'Flasks' in cls:
    text='30% increased Charge Recovery';rolled='30(25-30)% increased Charge Recovery'
   if cls=='Jewels':
    text='7% increased maximum Life';rolled='7(5-7)% increased maximum Life'
   mod='\n--------\n'+text;adv='\n--------\n{ Prefix Modifier "Healthy" (Tier: 1) — Life }\n'+rolled
   mp=[dict(Text=text,Kind='Explicit',AffixType='Unknown',Tier=None)]
   ma=[dict(Text=rolled,Kind='Explicit',AffixType='Prefix',Tier=1,AffixName='Healthy')]
  if j and i%5==0:
   tail+='\n--------\nNote: ~b/o 2.5 divine';common['Price']=dict(Amount=2.5,Currency='divine',Kind='~b/o')
  start=header+'\n--------\n'+props
  save(cls,base.lower().replace(' ','-')+f'-{j}',start+mod+tail,start+adv+tail,common,dict(Mods=mp),dict(Mods=ma))
extra=[('Stackable Currency','Chaos Orb','Currency','Stack Size: 1,234/5,000',dict(StackSize=dict(Current=1234,Maximum=5000))),('Map Fragments','Sacrifice at Dusk','Normal','Stack Size: 2/10',dict(StackSize=dict(Current=2,Maximum=10))),('Skill Gems','Fireball','Gem','Level: 20 (Max)\nQuality: +20%\nExperience: 1,000/2,000',dict(GemLevel=20,Quality=20,GemExperience=dict(Current=1000,Maximum=2000))),('Support Gems','Added Fire Damage Support','Gem','Level: 1\nExperience: 0/100',dict(GemLevel=1)),('Divination Cards','The Scholar','Divination Card','Stack Size: 3/3',dict(StackSize=dict(Current=3,Maximum=3))),('Expedition Logbooks','Expedition Logbook','Rare','Item Level: 83',dict(ItemLevel=83))]
for cls,name,rarity,props,expect in extra:
 header=f'Item Class: {cls}\nRarity: {rarity}\n{name}\n--------\n{props}'
 save(cls,name.lower().replace(' ','-'),header,header,dict(ItemClass=cls,Name=name,**expect))
