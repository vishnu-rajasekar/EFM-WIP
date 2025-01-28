import Rhino
import Grasshopper 
import scriptcontext as sc

class MyComponent:

    @staticmethod
    def RunScript(gh_doc, component):
        elemets = MyComponent.check_dict()
        comps = MyComponent.check_doc(gh_doc)
        return comps

    def check_dict():
        # Get the elements from the sticky dictionary
        elements = sc.sticky.get("elements", [])       
        if len(elements) == 0:
            print("No elements found")
        else:
            return elements
    def check_doc(gh_doc):
        obj_list = []
        for obj in gh_doc.Objects:
            obj_list.append(obj.NickName)
            
        return obj_list
    
