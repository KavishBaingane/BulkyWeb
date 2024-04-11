$(document).ready(function () {
    loadDataTable();
})

function loadDataTable() {
    dataTable = $('#tblData').DataTable({
        "ajax": {
            url: '/admin/product/getAll'
        },

        "columns": [
            { data: 'title', "width": "25p%" },
            { data: 'isbn', "width": "15p%" },
            { data: 'listPrice', "width": "10p%" },
            { data: 'author', "width": "20p%" },
            { data: 'category.name', "width": "15p%" },
            {
                data: 'id', "render": function (data) {
                    return `<div class="w-75 btn-group role="group">
                     <a href="/admin/product/upsert?id=d" class="btn btn-primary mx-2"><i class="bi bi-pencil-square"><i>Edit></a>
                     <a href="" class="btn btn-primary mx-2"><i class="bi bi-trash-fill"><i>Delete></a>
                    </div>`
                },
                "width": "15%"
            }


        ]
    });

}
