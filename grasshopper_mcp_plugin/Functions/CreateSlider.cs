using System;
using System.Drawing;
using Grasshopper.Kernel.Special;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace GrasshopperMCP.Functions;

public partial class GrasshopperMCPFunctions
{
    public JObject CreateSlider(JObject parameters)
    {
        var results = new JObject();
        var component = new GH_NumberSlider();
        component.SetInitCode("0.0 < 0.5 < 1.0");
        

        component.Attributes.Pivot = new System.Drawing.PointF((float)100, (float)100);
                        
        // 添加到文檔
        doc.AddObject(component, false);
                        
        // 刷新畫布
        doc.NewSolution(false);
                        
        // 返回組件信息
        return new JObject()
        {
            ["id"] = component.InstanceGuid.ToString(),
            ["type"] = component.GetType().Name,
            ["name"] = component.NickName,
            ["x"] = component.Attributes.Pivot.X,
            ["y"] = component.Attributes.Pivot.Y
        };
    }
}