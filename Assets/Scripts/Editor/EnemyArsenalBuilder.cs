using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Rebuildable authoring pipeline for the supplied hostile projectile artwork.</summary>
public static class EnemyArsenalBuilder
{
    const string BulletFolder = "Assets/Prefabs/EnemyBullets";
    const string SpriteFolder = "Assets/Sprites/Bullets/Prepared";
    static readonly string[] Families = {"DroneFighter", "LaserTurret", "Mine", "Bomber", "Sniper", "Swarmer", "ShieldUnit", "HeavyCruiser", "TwinCannon", "MissileShip", "Stinger", "Wasp", "Dreadnought", "Alien", "Boss"};
    static readonly string[] Enemies = {"Enemy_Scout", "Enemy_Gunship", "Enemy_Fighter", "Enemy_Striker", "Enemy_Cruiser", "Enemy_Swarmer", "Enemy_ShieldUnit", "Enemy_Destroyer", "Enemy_TwinCannon", "Enemy_MissileShip", "Enemy_Elite", "Enemy_Wasp", "Enemy_MiniBoss", "Enemy_AlienBoss", "Enemy_Boss"};
    static readonly int[] ShipSprites = {14,15,17,18,13,24,26,28,30,32,46,47,49,50,53};
    static readonly float[] Speeds = {4.2f,5.7f,2.8f,3.5f,6.5f,3.7f,3.3f,4.1f,4.8f,3.2f,5.2f,4.6f,3.8f,3.5f,3.5f};
    static readonly float[] Rates = {2.6f,2.4f,3.1f,2.9f,3.2f,2.8f,3.2f,2.5f,2.7f,3.1f,2.3f,2.4f,2.1f,2.3f,2.2f};
    // Values match EnemyFirePattern. Per hull these override a wave's generic fallback style.
    static readonly int[] FireStyles = {0,0,3,1,2,5,2,1,0,6,7,1,4,7,3};

    [MenuItem("GalaxyStrike/Enemy Arsenal/Rebuild Prefabs and Bind Enemies")]
    public static void Build()
    {
        Directory.CreateDirectory(BulletFolder);
        Directory.CreateDirectory(SpriteFolder);
        AssetDatabase.Refresh();
        var report = new List<string>();
        for (int family = 0; family < Families.Length; family++)
        {
            string name = Families[family];
            var texture = new Texture2D(2,2,TextureFormat.RGBA32,false);
            ImageConversion.LoadImage(texture, File.ReadAllBytes("Assets/Sprites/Bullets/"+name+"Bullets.png"));
            var cells = Cells(name);
            var projectiles = new List<GameObject>();
            for (int i = 0; i < cells.Length; i++)
            {
                string stem = name + "_" + (i+1).ToString("00");
                string spritePath = SpriteFolder+"/"+stem+".png";
                PrepareSprite(texture,cells[i],spritePath);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                bool missile = name == "MissileShip" || ((name == "Bomber" || name == "Dreadnought") && i == cells.Length-1);
                bool round = sprite.rect.width / sprite.rect.height > 0.72f && !missile;
                var root = new GameObject("EnemyBullet_"+stem);
                try
                {
                    root.tag = "EnemyBullet";
                    var rb = root.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    rb.gravityScale = 0f;
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    var art = new GameObject("Visual"); art.transform.SetParent(root.transform,false);
                    var renderer = art.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.sortingOrder = 25;
                    var legacy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_Bullet.prefab");
                    renderer.sharedMaterial = legacy.GetComponent<SpriteRenderer>().sharedMaterial;
                    float length = round ? 0.32f : missile ? 0.56f : name=="Sniper" || name=="LaserTurret" ? 0.66f : 0.48f;
                    float scale = length / Mathf.Max(sprite.bounds.size.x,sprite.bounds.size.y);
                    art.transform.localScale = Vector3.one * scale;
                    float w = sprite.bounds.size.x * scale, h = sprite.bounds.size.y * scale;
                    if (round)
                    {
                        var hit = root.AddComponent<CircleCollider2D>(); hit.isTrigger = true;
                        hit.radius = Mathf.Min(w,h)*0.28f;
                    }
                    else
                    {
                        var hit = root.AddComponent<CapsuleCollider2D>(); hit.isTrigger = true;
                        hit.direction = CapsuleDirection2D.Vertical;
                        hit.size = new Vector2(Mathf.Clamp(w*0.52f,0.045f,0.16f),h*0.62f);
                    }
                    var bullet = root.AddComponent<EnemyBullet>();
                    var so = new SerializedObject(bullet);
                    so.FindProperty("speed").floatValue = Speeds[family] * (round ? 0.85f : 1f);
                    so.FindProperty("lifetime").floatValue = 7f;
                    so.FindProperty("visual").objectReferenceValue = art.transform;
                    so.FindProperty("spinSpeed").floatValue = round ? 95f : 0f;
                    so.FindProperty("homingDuration").floatValue = missile ? 0.8f : 0f;
                    so.FindProperty("turnSpeed").floatValue = 50f;
                    so.FindProperty("acceleration").floatValue = missile ? 1.1f : 0f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    projectiles.Add(PrefabUtility.SaveAsPrefabAsset(root,BulletFolder+"/"+root.name+".prefab"));
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            UnityEngine.Object.DestroyImmediate(texture);
            foreach (string folder in new[]{"Assets/Prefabs", "Assets/Resources/Prefabs"})
                BindEnemy(folder,family,projectiles);
            report.Add(name+": "+projectiles.Count+" projectiles -> "+Enemies[family]);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[EnemyArsenal] Ready.\n"+string.Join("\n",report));
    }

    static Rect[] Cells(string family)
    {
        if (family=="Dreadnought") return new[]{new Rect(0,0.45f,0.4f,0.55f),new Rect(0.4f,0.51f,0.6f,0.49f),new Rect(0,0,0.48f,0.45f),new Rect(0.48f,0,0.52f,0.51f)};
        if (family=="Alien" || family=="Boss") return new[]{new Rect(0,0,0.25f,1),new Rect(0.25f,0,0.14f,1),new Rect(0.39f,0,0.2f,1),new Rect(0.59f,0,0.28f,1),new Rect(0.87f,0,0.13f,1)};
        if (family=="DroneFighter") return new[]{new Rect(0.10f,0,0.23f,1),new Rect(0.39f,0,0.115f,1),new Rect(0.505f,0,0.115f,1),new Rect(0.68f,0,0.3f,1)};
        return new[]{new Rect(0,0,0.365f,1),new Rect(0.365f,0,0.315f,1),new Rect(0.68f,0,0.32f,1)};
    }

    static void PrepareSprite(Texture2D source, Rect cell, string path)
    {
        int x0=Mathf.RoundToInt(cell.x*source.width), y0=Mathf.RoundToInt(cell.y*source.height);
        int x1=Mathf.Min(source.width,Mathf.RoundToInt(cell.xMax*source.width));
        int y1=Mathf.Min(source.height,Mathf.RoundToInt(cell.yMax*source.height));
        int minX=x1, minY=y1, maxX=x0, maxY=y0;
        var pixels=source.GetPixels32();
        var visited=new bool[pixels.Length];
        var queue=new Queue<int>();
        int largest=0;
        for(int y=y0;y<y1;y++) for(int x=x0;x<x1;x++)
        {
            int seed=y*source.width+x;
            if(visited[seed] || pixels[seed].a<80)continue;
            visited[seed]=true;queue.Enqueue(seed);
            int count=0, left=x, right=x, bottom=y, top=y;
            while(queue.Count>0)
            {
                int index=queue.Dequeue(), px=index%source.width, py=index/source.width;
                count++;left=Mathf.Min(left,px);right=Mathf.Max(right,px);bottom=Mathf.Min(bottom,py);top=Mathf.Max(top,py);
                for(int n=0;n<4;n++)
                {
                    int nx=px+(n==0?-1:n==1?1:0), ny=py+(n==2?-1:n==3?1:0);
                    if(nx<x0||nx>=x1||ny<y0||ny>=y1)continue;
                    int next=ny*source.width+nx;
                    if(visited[next]||pixels[next].a<80)continue;
                    visited[next]=true;queue.Enqueue(next);
                }
            }
            if(count>largest){largest=count;minX=left;maxX=right;minY=bottom;maxY=top;}
        }
        minX=Mathf.Max(x0,minX-3); minY=Mathf.Max(y0,minY-3);
        maxX=Mathf.Min(x1-1,maxX+3); maxY=Mathf.Min(y1-1,maxY+3);
        var crop = new Texture2D(maxX-minX+1,maxY-minY+1,TextureFormat.RGBA32,false);
        crop.SetPixels(source.GetPixels(minX,minY,crop.width,crop.height)); crop.Apply();
        File.WriteAllBytes(path,crop.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(crop);
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100;
        importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;
        importer.filterMode=FilterMode.Bilinear;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=256;
        importer.SaveAndReimport();
    }

    static void BindEnemy(string folder,int family,List<GameObject> projectiles)
    {
        string path=folder+"/"+Enemies[family]+".prefab";
        bool exists=AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null;
        string template=family==13 ? "Enemy_Boss" : family==6 || family==8 || family==9 ? "Enemy_Destroyer" : "Enemy_Scout";
        var root=PrefabUtility.LoadPrefabContents(exists ? path : folder+"/"+template+".prefab");
        try
        {
            root.name=Enemies[family];
            var enemy=root.GetComponent<Enemy>();
            var so=new SerializedObject(enemy);
            so.FindProperty("canShoot").boolValue=true;
            so.FindProperty("bulletPrefab").objectReferenceValue=projectiles[0];
            so.FindProperty("fireRate").floatValue=Rates[family];
            so.FindProperty("twinMuzzles").boolValue=family==8;
            so.FindProperty("preferAimedShots").boolValue=family==4 || family==9;
            so.FindProperty("useArchetypeFirePattern").boolValue=true;
            so.FindProperty("archetypeFirePattern").enumValueIndex=FireStyles[family];
            so.FindProperty("burstShots").intValue=family==9?2:family==10?4:3;
            so.FindProperty("burstInterval").floatValue=family==10?0.16f:0.13f;
            if(family==4 || family==1)
            {
                var warning=root.transform.Find("AimWarning");
                if(warning==null){warning=new GameObject("AimWarning").transform;warning.SetParent(root.transform,false);}
                var line=warning.GetComponent<LineRenderer>();
                if(line==null)line=warning.gameObject.AddComponent<LineRenderer>();
                line.sharedMaterial=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_Bullet.prefab").GetComponent<SpriteRenderer>().sharedMaterial;
                line.useWorldSpace=true;line.positionCount=2;line.startWidth=0.018f;line.endWidth=0.008f;
                line.startColor=new Color(1f,0.2f,0.5f,0.38f);line.endColor=new Color(1f,0.2f,0.5f,0.12f);
                line.sortingOrder=24;line.enabled=false;
                so.FindProperty("aimWarning").objectReferenceValue=line;
            }
            var variants=so.FindProperty("bulletVariants");variants.arraySize=4;
            // Orb artwork is used for radial patterns; the long weapons remain directional.
            int orb=family==0 ? 3 : family==1 || family==2 || family==6 ? 1 : family==13 || family==14 ? 2 : family==12 ? 1 : 2;
            int[] indices={0,Mathf.Min(1,projectiles.Count-1),orb,projectiles.Count-1};
            for(int i=0;i<4;i++) variants.GetArrayElementAtIndex(i).objectReferenceValue=projectiles[indices[i]];
            so.ApplyModifiedPropertiesWithoutUndo();
            if(!exists)
            {
                var sprites=AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/listEnemy_removebg.png").OfType<Sprite>();
                var sprite=sprites.FirstOrDefault(s=>s.name=="listEnemy_removebg_"+ShipSprites[family]);
                if(sprite==null) throw new InvalidOperationException("Missing ship sprite "+family);
                var renderer=root.GetComponent<SpriteRenderer>();renderer.sprite=sprite;
                float size=family==13 ? 1.8f : 0.85f;
                root.transform.localScale=Vector3.one*(size/Mathf.Max(sprite.bounds.size.x,sprite.bounds.size.y));
                var box=root.GetComponent<BoxCollider2D>();
                if(box!=null){box.offset=sprite.bounds.center;box.size=(Vector2)sprite.bounds.size*0.72f;}
                var circle=root.GetComponent<CircleCollider2D>();
                if(circle!=null){circle.offset=sprite.bounds.center;circle.radius=Mathf.Min(sprite.bounds.size.x,sprite.bounds.size.y)*0.34f;}
                so.Update();so.FindProperty("enemyName").stringValue=Enemies[family];so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();
                so.FindProperty("health").intValue=family==13 ? 180 : family==6 ? 5 : family==8 || family==9 ? 4 : 2;
                so.FindProperty("scoreValue").intValue=family==13 ? 2000 : 150;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
