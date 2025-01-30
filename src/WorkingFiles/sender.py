import Rhino.Geometry as rg
from Grasshopper.Kernel.Parameters import Param_Geometry, Param_Brep
from Grasshopper.Kernel.Special import GH_NumberSlider
from Grasshopper.Kernel.Data import GH_Path
from Grasshopper.Kernel.Types import GH_Brep
import Grasshopper  
import scriptcontext as sc
from System.Drawing import PointF
from System import Decimal
import uuid

class MyComponent:

    @staticmethod
    def RunScript(gh_doc, component):
        # 1. Retrieve the EFM data from sticky
        EFM = sc.sticky.get("EFM", {})
        elements = EFM.get("GeomDict", {}).get("Elements", [])
        
        # Track existing components by UUID
        component_tracking = sc.sticky.setdefault("ComponentTracking", {
            "params": {},
            "sliders": {}
        })
        
        # 1. Update or create components
        for elem in elements:
            elem_id = elem.get("id", str(uuid.uuid4()))  # Ensure ID exists
            elem_name = elem["name"]
            elem_thickness = elem["thickness"]
            
            # Handle geometry param
            if elem_id in component_tracking["params"]:
                # Update existing param
                geo_param = gh_doc.FindObject(component_tracking["params"][elem_id], False)
                if geo_param:
                    # Only update if name changed
                    if geo_param.NickName != elem_name:
                        geo_param.Name = elem_name
                        geo_param.NickName = elem_name
                    
                    # Update geometries
                    geo_param.PersistentData.ClearData()
                    for srf in elem["geometries"]:
                        geo_param.PersistentData.Append(GH_Brep(srf.DuplicateFace(True)))
            else:
                # Create new param
                geo_param = Param_Brep()
                geo_param.Name = elem_name
                geo_param.NickName = elem_name
                geo_param.Description = f"Geometry for {elem_name}"
                geo_param.CreateAttributes()
                
                # Set position (implement your layout logic here)
                x_pos = 100
                y_pos = 200
                geo_param.Attributes.Pivot = PointF(x_pos, y_pos)
                
                # Add geometries
                geo_param.PersistentData.ClearData()
                for srf in elem["geometries"]:
                    geo_param.PersistentData.Append(GH_Brep(srf.DuplicateFace(True)))
                
                gh_doc.AddObject(geo_param, False)
                component_tracking["params"][elem_id] = geo_param.InstanceGuid
            
            # Handle slider
            if elem_id in component_tracking["sliders"]:
                # Update existing slider
                slider = gh_doc.FindObject(component_tracking["sliders"][elem_id], False)
                if slider:
                    slider.Slider.Value = Decimal(float(elem_thickness))
            else:
                # Create new slider
                slider = GH_NumberSlider()
                slider.Name = f"{elem_name}_Thickness"
                slider.NickName = f"{elem_name}_Thickness"
                slider.Slider.Minimum = Decimal(0.0)
                slider.Slider.Maximum = Decimal(10000.0)
                slider.Slider.Value = Decimal(float(elem_thickness))
                slider.CreateAttributes()
                
                # Position next to param (implement your layout logic)
                if elem_id in component_tracking["params"]:
                    param = gh_doc.FindObject(component_tracking["params"][elem_id], False)
                    if param:
                        slider.Attributes.Pivot = PointF(
                            param.Attributes.Pivot.X + 150,
                            param.Attributes.Pivot.Y
                        )
                
                gh_doc.AddObject(slider, False)
                component_tracking["sliders"][elem_id] = slider.InstanceGuid
        
        # 2. Cleanup deleted elements
        current_ids = {elem.get("id", "") for elem in elements}
        
        # Cleanup params
        for elem_id in list(component_tracking["params"].keys()):
            if elem_id not in current_ids:
                param = gh_doc.FindObject(component_tracking["params"][elem_id], False)
                if param: 
                    gh_doc.RemoveObject(param, False)
                del component_tracking["params"][elem_id]
        
        # Cleanup sliders
        for elem_id in list(component_tracking["sliders"].keys()):
            if elem_id not in current_ids:
                slider = gh_doc.FindObject(component_tracking["sliders"][elem_id], False)
                if slider: 
                    gh_doc.RemoveObject(slider, False)
                del component_tracking["sliders"][elem_id]
        
        sc.sticky["ComponentTracking"] = component_tracking
        
        # Force solution refresh
        def on_solution_end(doc):
            doc.NewSolution(False)
        
        gh_doc.ScheduleSolution(1, on_solution_end)
        
        return elements