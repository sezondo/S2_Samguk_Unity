using UnityEngine;

/// <summary>원본 자세를 보존하면서 부위별 강체 관절과 부드러운 연결 가중치로 대기한다.</summary>
public sealed class StandingPoseMesh : MonoBehaviour
{
    // 원본 그림과 명시적인 원화 재질이다. 픽셀은 수정하지 않는다.
    public Texture2D source;
    public Material material;
    // 편집기에서 명시적으로 연결하는 표시 메시 컴포넌트다.
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;
    // 인물 높이와 사용자가 선택한 움직임 강도다.
    public float height = 3.2f;
    public float strength = 2.5f;
    public bool animate = true;
    public bool manualEvaluation;
    // 확정 자세의 관절 경계와 무기 강체 데이터를 명시적으로 연결한다.
    public StandingPoseData profile;
    // 발 고정·몸통·머리·총 팔·손과 총·앞 팔·양쪽 옷자락·호스 순서의 관절이다.
    private readonly Matrix4x4[] pose = new Matrix4x4[9];
    private Mesh mesh;
    private Vector3[] rest, moved;
    private float[,] weights;
    private Vector2[] coordinates;
    private int columns = 64, rows = 90;
    private float width;
    // 생성된 표시 재질과 원화 로컬 정렬 정보다.
    public Vector3 SpriteLocalOffset { get; private set; }
    private Material runtimeMaterial;
    public Mesh RuntimeMesh => mesh;
    public Vector3[] RestVertices => rest;
    public Vector2[] Coordinates => coordinates;

    /// <summary>원본의 투명 여백을 제외하고 고정 UV 메시와 관절 가중치를 만든다.</summary>
    private void Start(){Initialize();}

    /// <summary>비교 전환과 캡처에서 필요한 메시를 최초 한 번만 준비한다.</summary>
    public void Initialize()
    {
        if(mesh!=null&&rest!=null&&moved!=null&&weights!=null&&coordinates!=null)return;
        // 편집기 재로딩은 다차원 가중치를 복원하지 않으므로 남은 메시와 함께 다시 준비한다.
        if(mesh!=null)Destroy(mesh);mesh=null;
        
        if(runtimeMaterial!=null)Destroy(runtimeMaterial);
        if (!HasValidReference() || !HasValidData())
        { Debug.LogError("스탠딩 아이들에는 읽기 가능한 원화와 재질이 필요합니다.", this); enabled = false; return; }
        if(profile.anchors==null||profile.anchors.Length!=9||profile.regions==null)
        {Debug.LogError("부위별 자세 프로필의 관절 또는 경계가 없습니다.",this);enabled=false;return;}
        var pixels = source.GetPixels32(); int l=source.width,r=-1,b=source.height,t=-1;
        for(int y=0;y<source.height;y++) for(int x=0;x<source.width;x++) if(pixels[y*source.width+x].a>16)
        {l=Mathf.Min(l,x);r=Mathf.Max(r,x);b=Mathf.Min(b,y);t=Mathf.Max(t,y);}
        if(r<l){Debug.LogError("스탠딩 원화에 보이는 픽셀이 없습니다.",this);enabled=false;return;}
        height=(t-b+1f)/profile.sprite.pixelsPerUnit;
        width=(r-l+1f)/profile.sprite.pixelsPerUnit;
        SpriteLocalOffset=new Vector3((l+(r-l+1f)*.5f-profile.sprite.pivot.x)/profile.sprite.pixelsPerUnit,(b-profile.sprite.pivot.y)/profile.sprite.pixelsPerUnit,0);
        int count=(columns+1)*(rows+1);rest=new Vector3[count];moved=new Vector3[count];coordinates=new Vector2[count];weights=new float[count,9];
        var uv=new Vector2[count];var triangles=new int[columns*rows*6];int index=0;
        for(int y=0;y<=rows;y++) for(int x=0;x<=columns;x++)
        {
            int i=y*(columns+1)+x;float nx=x/(float)columns,ny=y/(float)rows;
            coordinates[i]=new Vector2(nx,ny);rest[i]=Local(nx,ny);
            uv[i]=new Vector2(Mathf.Lerp(l,r+1,nx)/source.width,Mathf.Lerp(b,t+1,ny)/source.height);
            AssignWeights(i,new Vector2(nx*850,(1-ny)*1187));
        }
        for(int y=0;y<rows;y++) for(int x=0;x<columns;x++)
        {int i=y*(columns+1)+x;triangles[index++]=i;triangles[index++]=i+columns+1;triangles[index++]=i+1;triangles[index++]=i+1;triangles[index++]=i+columns+1;triangles[index++]=i+columns+2;}
        mesh=new Mesh{name="원화 보존 부위별 아이들"};mesh.MarkDynamic();mesh.vertices=rest;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();
        runtimeMaterial=new Material(material){name=material.name+" 표시용"};runtimeMaterial.mainTexture=source;
        meshFilter.sharedMesh=mesh;meshRenderer.sharedMaterial=runtimeMaterial;
        Evaluate(0);
    }

    /// <summary>부위마다 다른 지연을 가진 호흡을 진행한다.</summary>
    private void Update(){if(!manualEvaluation)Evaluate(Time.time);}

    /// <summary>정해진 시각의 부위별 호흡을 계산하며 무기와 머리 내부는 강체로 유지한다.</summary>
    public void Evaluate(float time)
    {
        if(mesh==null||rest==null||moved==null||weights==null){Initialize();if(mesh==null||weights==null)return;}
        float amount=animate?strength*(profile!=null?profile.motionScale:1):0;
        float phase=time*Mathf.PI*2/3.8f+(profile!=null?profile.phaseOffset:0),breath=Mathf.Sin(phase),lag=Mathf.Sin(phase-.40f),slow=Mathf.Sin(phase*.5f);
        pose[0]=Matrix4x4.identity;
        pose[1]=Around(.52f,.56f,.10f*breath*amount,new Vector3(.00055f*slow*height,.0027f*breath*height,0)*amount);
        pose[2]=pose[1]*Around(.54f,.76f,-.18f*lag*amount,new Vector3(0,-.00065f*lag*height*amount,0));
        pose[3]=pose[1]*Around(.29f,.72f,.16f*Mathf.Sin(phase-.25f)*amount,Vector3.zero);
        pose[4]=pose[3]*Around(.21f,.43f,-.12f*Mathf.Sin(phase-.65f)*amount,Vector3.zero);
        pose[5]=pose[1]*Around(.69f,.62f,-.25f*Mathf.Sin(phase-.48f)*amount,Vector3.zero);
        pose[6]=Around(.32f,.44f,.32f*Mathf.Sin(phase-.85f)*amount,new Vector3(0,.0004f*slow*height*amount,0));
        pose[7]=Around(.64f,.46f,-.23f*Mathf.Sin(phase-.95f)*amount,Vector3.zero);
        pose[8]=pose[1]*Around(.18f,.63f,.12f*Mathf.Sin(phase-.7f)*amount,Vector3.zero);
        if(profile!=null)
        {
            var a=profile.anchors;
            pose[1]=Around(a[1].x,a[1].y,.10f*breath*amount,new Vector3(profile.wallCover?0:.00055f*slow*height,.0027f*breath*height,0)*amount);
            pose[2]=pose[1]*Around(a[2].x,a[2].y,-.18f*lag*amount,new Vector3(0,-.00065f*lag*height*amount,0));
            pose[3]=pose[1]*Around(a[3].x,a[3].y,.16f*Mathf.Sin(phase-.25f)*amount,Vector3.zero);
            pose[4]=pose[3]*Around(a[4].x,a[4].y,-.12f*Mathf.Sin(phase-.65f)*amount,Vector3.zero);
            pose[5]=pose[1]*Around(a[5].x,a[5].y,-.25f*Mathf.Sin(phase-.48f)*amount,Vector3.zero);
            pose[6]=Around(a[6].x,a[6].y,.32f*Mathf.Sin(phase-.85f)*amount,Vector3.zero);
            pose[7]=Around(a[7].x,a[7].y,-.23f*Mathf.Sin(phase-.95f)*amount,Vector3.zero);
            pose[8]=pose[1]*Around(a[8].x,a[8].y,.12f*Mathf.Sin(phase-.7f)*amount,Vector3.zero);
        }
        for(int i=0;i<rest.Length;i++)
        {
            Vector3 vertex=Vector3.zero;
            for(int bone=0;bone<9;bone++) if(weights[i,bone]>0) vertex+=pose[bone].MultiplyPoint3x4(rest[i])*weights[i,bone];
            moved[i]=vertex;
        }
        mesh.vertices=moved;mesh.RecalculateBounds();
    }

    /// <summary>지정 부위의 회전 중심을 유지하는 강체 변환을 만든다.</summary>
    private Matrix4x4 Around(float x,float y,float degrees,Vector3 shift)
    {Vector3 p=Local(x,y);return Matrix4x4.Translate(p+shift)*Matrix4x4.Rotate(Quaternion.Euler(0,0,degrees))*Matrix4x4.Translate(-p);}

    /// <summary>정규화된 그림 좌표를 발 고정 로컬 좌표로 변환한다.</summary>
    private Vector3 Local(float x,float y){return new Vector3((x-.5f)*width,y*height,0);}

    /// <summary>부위별 원화 경계에 연결 가중치를 주되 손·총·머리 내부는 한 관절로 묶는다.</summary>
    private void AssignWeights(int i,Vector2 p)
    {
        weights[i,0]=1;
        foreach(var region in profile.regions)Overlay(i,region.bone,Region(p,region.polygon,region.feather));
        if(p.y>(1-profile.fixedLowerFraction)*1187)for(int bone=0;bone<9;bone++)weights[i,bone]=bone==0?1:0;
        ApplyRigidRegions(i,p);
    }

    /// <summary>강체 경계 바깥에 두 셀 정도 여유를 주어 무기를 가로지르는 삼각형도 고정한다.</summary>
    private void ApplyRigidRegions(int i,Vector2 p)
    {
        if(profile.rigidRegions==null)return;
        foreach(var region in profile.rigidRegions)if(Region(p,region.polygon,region.feather)>0)SetRigidWeight(i,region.bone);
    }

    /// <summary>무기 전체의 가중치를 하나의 관절로 고정한다.</summary>
    private void SetRigidWeight(int i,int bone){for(int j=0;j<9;j++)weights[i,j]=j==bone?1:0;}

    /// <summary>무기 강체 영역의 모든 정점을 해당 관절 변환과 대조한다.</summary>
    public float MaximumRigidError()
    {
        float maximum=0;
        for(int i=0;i<rest.Length;i++)for(int bone=0;bone<9;bone++)if(weights[i,bone]==1)
            maximum=Mathf.Max(maximum,Vector3.Distance(moved[i],pose[bone].MultiplyPoint3x4(rest[i])));
        return maximum;
    }

    /// <summary>무기·접점 검사 위치가 해당 관절을 따를 때의 변환 결과를 반환한다.</summary>
    public Vector3 ExpectedBonePoint(int index,int bone){return pose[bone].MultiplyPoint3x4(rest[index]);}

    /// <summary>새 부위의 가중치가 이전 부위를 자연스럽게 덮도록 정규화한다.</summary>
    private void Overlay(int i,int bone,float alpha)
    {for(int j=0;j<9;j++)weights[i,j]*=1-alpha;weights[i,bone]+=alpha;}

    /// <summary>다각형의 안팎과 경계 거리를 사용해 이음새를 부드럽게 연결한다.</summary>
    private static float Region(Vector2 p,Vector2[] polygon,float feather)
    {
        bool inside=false;float distance=float.PositiveInfinity;
        for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
        {
            Vector2 a=polygon[j],b=polygon[i],edge=b-a;
            float t=Mathf.Clamp01(Vector2.Dot(p-a,edge)/edge.sqrMagnitude);distance=Mathf.Min(distance,Vector2.Distance(p,a+t*edge));
            if((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
        }
        float value=Mathf.Clamp01(.5f+(inside?distance:-distance)/(2*feather));return value*value*(3-2*value);
    }

    /// <summary>그림 내부의 특정 부위를 검사할 가장 가까운 정점 번호를 찾는다.</summary>
    public int Nearest(float x,float y)
    {int best=0;float distance=float.PositiveInfinity;for(int i=0;i<coordinates.Length;i++){float d=(coordinates[i]-new Vector2(x,y)).sqrMagnitude;if(d<distance){distance=d;best=i;}}return best;}

    /// <summary>검증할 부위 내부에서 완전히 같은 관절을 사용하는 두 정점을 찾는다.</summary>
    public bool FindRigidPair(int bone,out int first,out int last)
    {
        first=last=-1;for(int i=0;i<rest.Length;i++)if(weights[i,bone]>.99999f){if(first<0)first=i;last=i;}
        return first>=0&&last!=first;
    }

    /// <summary>필수 표시 컴포넌트의 명시적 연결을 검사한다.</summary>
    public bool HasValidReference()
    {
        if(meshFilter!=null&&meshRenderer!=null&&material!=null)return true;
        Debug.LogError("대기 메시의 표시 컴포넌트 또는 재질이 연결되지 않았습니다.",this);return false;
    }

    /// <summary>실제 사용할 원화와 관절 데이터가 정상인지 검사한다.</summary>
    public bool HasValidData()
    {
        if(profile==null||profile.sprite==null||source==null||source!=profile.source||!source.isReadable||profile.anchors==null||profile.anchors.Length!=9||profile.regions==null||profile.rigidRegions==null||profile.motionScale<=0||strength<0)
        {Debug.LogError("대기 메시의 확정 원화 또는 자세 데이터가 누락됐습니다.",this);return false;}
        foreach(var region in profile.regions)if(!ValidRegion(region))return false;
        foreach(var region in profile.rigidRegions)if(!ValidRegion(region))return false;
        return true;
    }

    /// <summary>부위의 관절 번호와 다각형 경계를 검사한다.</summary>
    private bool ValidRegion(StandingPoseData.Region region)
    {
        if(region!=null&&region.bone>=0&&region.bone<9&&region.polygon!=null&&region.polygon.Length>=3&&region.feather>0)return true;
        Debug.LogError("대기 자세의 부위 경계가 잘못됐습니다.",this);return false;
    }

    /// <summary>런타임 메시와 재질을 정리한다.</summary>
    private void OnDestroy(){if(mesh!=null)Destroy(mesh);if(runtimeMaterial!=null)Destroy(runtimeMaterial);}
}
