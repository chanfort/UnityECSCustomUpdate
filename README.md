# Unity ECS Custom Update

Custom update often refers to the update which runs calculations at different rate compared the default update. This can be useful if calculations are expensive and could be performed at lower frequency. So custom update spreads calculation load over smaller number of array elements for given update.

This project contains implementation of custom updates for various scenarios in ECS, such as using in IJobParallerFor, IJobChunk, etc.

This project was inspired from Unity forum discussions available here https://forum.unity.com/threads/using-minimum-and-maximum-array-indices-in-ijobparallelfor.566974/
