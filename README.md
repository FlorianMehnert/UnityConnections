# UnityConnectors

The goal of this project is to create an overview over UnityProjects to get an intuitive understanding of complicated references among Monobehaviours

## Progress

- These are the current implementations of this project:
  - Rectangle Graph generates a graph of instances of classes and connects them as they have references of each other
  - clicking on a rectangle opens its contents in a second split view

![Rectangle Graph](images/rectangle_graph.png)

- Unities [experimental graph view api](https://docs.unity3d.com/ScriptReference/Experimental.GraphView.GraphView.html) allows to rely on already implemented nodes from unity and connects nodes similar to the rectangle graph

![Graph View Api](images/graph_view_api.png)