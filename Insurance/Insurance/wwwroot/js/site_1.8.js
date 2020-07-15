// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.
// Write your JavaScript code.
var prevFile = new Array();
var screenWidth = window.screen.width;
var screenHeight = window.screen.height;
var imgLoadedFlag = false;
var base64String = '';
var compressed = false;
var btnSubmit = document.getElementById('btnSubmit');
var filesCompressed = 0;
var currentLiIndex = 0;
function uploadImage() {
    $("#uploadImages").click();
}

function ShowAccountDetail(userName, isSelf) {
    $("#CompanyName").attr("readonly", true);
    $("#CompanyNameAbb").attr("readonly", true);

    $("#RecipeCompany").attr("readonly", true);
    $("#TaxNum").attr("readonly", true);
    $("#RecipeBank").attr("readonly", true);
    $("#RecipeAccount").attr("readonly", true);
    $("#RecipeAddress").attr("readonly", true);
    $("#RecipeType").attr("readonly", true);
    $.ajax({
        async: true,
        contentType: false,
        dataType: "json",
        processData: false,
        type: "get",
        url: "/Account/GetAccountDetail?userName=" + userName,
        success: function (data) {
            if (data == "fail") {
                alert("当前账号权限不足");
                return;
            }
            $("#Name").val(data.name);
            $("#Telephone").val(data.telephone);
            $("#Mail").val(data.mail);
            $("#CompanyName").val(data.companyName);
            $("#CompanyNameAbb").val(data.companyNameAbb);

            $("#RecipeCompany").val(data.recipeCompany);
            $("#TaxNum").val(data.taxNum);
            $("#RecipeBank").val(data.recipeBank);
            $("#RecipeAccount").val(data.recipeAccount);
            $("#RecipeAddress").val(data.recipeAddress);
            $("#RecipePhone").val(data.recipePhone);
            $("#recipettype").val(data.recipeType);
            if (!isSelf) {
                $("#CompanyName").attr("readonly", false);
                $("#CompanyNameAbb").attr("readonly", false);

                $("#RecipeCompany").attr("readonly", false);
                $("#TaxNum").attr("readonly", false);
                $("#RecipeBank").attr("readonly", false);
                $("#RecipeAccount").attr("readonly", false);
                $("#RecipeAddress").attr("readonly", false);
                $("#RecipeType").attr("readonly", false);
            }
        },
        fail: function (data) {
            alert("请求失败");
        },
        error: function (data) {
            alert("Unkonwn Error");
        }

    }
    );
}

function GetAccountInfo(userName, isSelf) {
    $("#baysbefore").attr("readonly", true);
    $("#plan").attr("readonly", true);

    $("#unitPrice").attr("readonly", true);
    $("#allowCreateAccount").attr("readonly", true);
    $.ajax({
        async: true,
        contentType: false,
        dataType: "json",
        processData: false,
        type: "get",
        url: "/Account/GetAccountDetail?userName=" + userName,
        success: function (data) {
            if (data == "fail") {
                alert("当前账号权限不足");
                return;
            }
            $("#daysbefore").val(data.daysBefore);
            $("#plan").val(data._Plan);
            $("#unitPrice").val(data.unitPrice);
            if (data.allowCreateAccount != "1") {
                $("#allowCreateAccount").val("不允许");
            } else {
                $("#allowCreateAccount").val("允许");
            }

            if (!isSelf) {
                $("#baysbefore").attr("readonly", false);
                $("#plan").attr("readonly", false);

                $("#unitPrice").attr("readonly", false);
                $("#allowCreateAccount").attr("readonly", false);
            }
        },
        fail: function (data) {
            alert("请求失败");
        },
        error: function (data) {
            alert("Unkonwn Error");
        }

    }
    );
}

function UpdateAccountInfo() {
    $.ajax({
        type: 'post',
        url: '/Account/UpdateAccountInfo',
        data: $("#form_updateAccountInfo").serialize(),
        dataType: "json",
        async: true,//默认异步，false-同步
        success: function (data) {
            if (data == true) {
                alert('保存成功');
            } else {
                alert('失败');
            }
        },
        fail: function (date) {
            alert('失败');
        }
    });
    $("#modal_close").click();
}

function SubmitRecipet(obj) {
    obj.disabled = !0;
    $.ajax(
        {
            async: true,
            data: $("#employeeChange").serialize(),
            datatype: 'json',
            type: 'post',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            url: '/EmployeeChange/UpdateSummary',
            contentType: false,
            processData: false,
            success: function (data) {
                if (data.length >= 1) {
                    obj.disabled = false;
                    if (data == "NotCalculated") {
                        alert("请先计算保费");
                    } else {
                        var start = document.getElementById('tr_startdate');
                        var end = document.getElementById('tr_enddate');
                        var desc = document.getElementById('description');
                        desc.innerText = data[0];
                        start.innerText = data[1];
                        end.innerText = data[2];
                    }
                }
            },
            fail: function () {
                obj.disabled = false;
                alert("失败");
            },
            error: function () {
                obj.disabled = false;
                alert("错误");
            }
        }
    );
}

function SelectAll(obj_selectall) {
    var checkBoxes = $('input[type="checkbox"]');
    var dom_cost = document.getElementById('totalCost');
    dom_cost.inner = '';
    for (i = 0; i < checkBoxes.length; i++) {
        if (checkBoxes[i] == obj_selectall) {
            continue;
        }
        var index = checkBoxes[i].name;
        var cost = document.getElementById("cost" + index).innerText;
        if (obj_selectall.checked) {
            if (checkBoxes[i].checked != obj_selectall.checked) {
                checkBoxes[i].checked = true;
                GetSelectedCost(checkBoxes[i], cost);
            }
        } else {
            if (checkBoxes[i].checked != obj_selectall.checked) {
                checkBoxes[i].checked = false;
                GetSelectedCost(checkBoxes[i], cost);
            }
        }

    }
}

function CalculateAll() {

}

function GetSelectedCost(obj, value) {
    var dom_cost = document.getElementById('totalCost');
    var cost = parseFloat(dom_cost.innerText);
    if (obj.checked) {
        cost += parseFloat(value);
    } else {
        cost -= parseFloat(value);
    }
    dom_cost.innerText = Math.round(cost * 100) / 100;
}

function MarkPaid() {
    var checkBoxes = $('input[type="checkbox"]:checked');
    var ids = new Array();
    var index = 0
    for (i = 0; i < checkBoxes.length; i++) {
        if (checkBoxes[i].id != "cbx_selectAll") {
            var folder = checkBoxes[i].parentNode.parentNode.children[6].innerText;
            ids[index] = folder + "#" + checkBoxes[i].id;
            index++;
        }
    }
    var fd = new FormData();
    fd.append('ids', ids);
    $.ajax(
        {
            async: true,
            data: fd,
            datatype: 'json',
            type: 'post',
            url: '/Home/MarkAsPaid',
            contentType: false,
            processData: false,
            success: function (data) {
                if (data == true) {
                    window.location.reload();
                    alert("结算成功");
                } else {
                    alert("结算失败");
                    window.location.reload();
                }
            },
        }
    );
}


function DeleteReceipt(name, file, date) {
    $.ajax(
        {
            url: "/Home/DeleteReceipt?company=" + name + "&filename=" + file + "&startdate=" + date,
            async: true,
            processData: false,
            type: 'get',
            dataType: 'JSON',
            success: function (data) {
                if (data == true) {
                    alert("删除成功！");
                    window.location.reload();
                } else {
                    alert(data);
                }
            },
            fail: function (data) {
                alert("删除失败");
            },
            error: function (data) {
                alert("错误");
            }
        }
    );
}

function DeleteCompanyData(company) {
    $.ajax(
        {
            url: "/Home/RemoveAccountData?accountName=" + company,
            async: true,
            processData: false,
            type: 'get',
            dataType: 'JSON',
            success: function (data) {
                if (data == true) {
                    alert("删除成功！");
                    window.location.reload();
                } else {
                    alert(data);
                }
            },
            fail: function (data) {
                alert("删除失败");
            },
            error: function (data) {
                alert("错误");
            }
        }
    );
}

function RegisterEvents() {
    $('#confirmDeleteExcel').on('show.bs.modal', function (event) {
        var button = $(event.relatedTarget); // Button that triggered the modal
        var company = button.data('company');
        var filename = button.data('filename');
        var uploaddate = button.data('uploaddate');
        var startdate = button.data('startdate');
        var modal = document.getElementById('confirmDeleteExcel');
        modal.getElementsByClassName('modal-body')[0].innerHTML = "是否确认删除于 " + uploaddate + " 上传的保单?";
        document.getElementById('btn_deletereceipt').onclick = function () { DeleteReceipt(company, filename, startdate) };
    });

    $('#confirmDeleteCompany').on('show.bs.modal', function (event) {
        var button = $(event.relatedTarget); // Button that triggered the modal
        var company = button.data('company');
        var modal = document.getElementById('confirmDeleteCompany');
        modal.getElementsByClassName('modal-body')[0].innerHTML = "是否确认删除 " + company + " 的所有保单数据?";
        document.getElementById('btn_ok').onclick = function () { DeleteCompanyData(company) };
    });


    $('#dataPreview').on('show.bs.modal', function (event) {
        var button = $(event.relatedTarget) // Button that triggered the modal
        var company = button.data('company') // Extract info from data-* attributes
        var filename = button.data('fn') // Extract info from data-* attributes
        var date = button.data('date') // Extract info from data-* attributes
        // If necessary, you could initiate an AJAX request here (and then do the updating in a callback).
        // Update the modal's content. We'll use jQuery here, but you could use a data binding library or other methods instead.
        var modal = $(this)
        modal.find('.modal-title').text('数据预览')
        var table = document.getElementById('previewTable');
        var table_header = table.getElementsByTagName('thead')[0];
        var table_body = table.getElementsByTagName('tbody')[0];
        table_header.innerHTML = '';
        table_body.innerHTML = '';
        document.getElementById('download').href = "/Home/DownloadExcel?company=" + company + "&fileName=" + filename + "&date=" + date;
        $.ajax(
            {
                async: true,
                processData: false,
                type: 'get',
                dataType: 'json',
                url: '/Home/PreviewTable?company=' + company + "&fileName=" + filename + "&date=" + date,
                success: function (data) {
                    if (data) {

                        var tr = document.createElement('tr');
                        var theaderRow = table_header.appendChild(tr);
                        for (var i = 0; i < Object.values(data[0]).length; i++) {
                            var th = document.createElement('th');
                            th.innerHTML = Object.values(data[0])[i];
                            theaderRow.appendChild(th);
                        }
                        for (var j = 1; j < data.length; j++) {
                            var tr = document.createElement('tr');
                            for (var k = 0; k < Object.values(data[j]).length; k++) {
                                var td = document.createElement('td');
                                td.innerHTML = Object.values(data[j])[k];
                                tr.appendChild(td);
                            }

                            table_body.appendChild(tr);
                        }
                    }
                },
                fail: function (data) {
                    alert("保存失败");
                },
                error: function (data) {
                    alert("错误");
                }
            }
        )

        modal.find('.modal-body input').val("test")
        modal.find('#download').href = "/Home/DownloadExcel?company=" + company + "&fileName=" + filename + "&date=" + date
    });
}



function SaveCost(company, obj) {
    if (!paidmoney_pattern.test(obj.value) && !paidmoney_pattern2.test(obj.value)) {
        alert("赔付金额不正确");
        obj.value = '';
        return;
    }
    $.ajax(
        {
            async: true,
            processData: false,
            type: 'get',
            dataType: 'json',
            url: '/Home/SaveCost?cost=' + obj.value + '&company=' + company,
            success: function (data) {
                if (data == true) {
                    alert("保存成功");
                    location.reload();
                }
            },
            fail: function (data) {
                alert("保存失败");
            },
            error: function (data) {
                alert("错误");
            }
        }
    );
}

function UpdatePersonalInfo() {

    $.ajax({
        type: 'post',
        url: '/Account/UpdatePersonalInfo',
        data: $("#Form_UserInfo").serialize(),
        dataType: "json",
        async: true,//默认异步，false-同步
        success: function (data) {
            if (data == true) {
                alert('保存成功');
            } else {
                alert('失败');
            }
        },
        fail: function (date) {
            alert('失败');
        }
    });
    $("#modal_close").click();
}

function SubmitAdd(obj) {
    if (obj.files.length > 1 || obj.files.length == 0) {
        return;
    }
    if (!CheckExcelType(obj.files)) {
        alert('文件格式不正确，请上传Excel文件')
    }
    var _formData = new FormData();
    _formData.append('mode', obj.id);
    for (var i = 0; i < obj.files.length; i++) {
        _formData.append('newExcel', obj.files[0]);
    }
    var dt = document.getElementById('invalidInfo');
    dt.innerHTML = '';
    $.ajax({
        type: 'post',
        url: '/EmployeeChange/UpdateEmployees',
        data: _formData,
        async: false,
        contentType: false,
        dataType: false,//必须false才会自动加上正确的Content-Type
        processData: false,
        success: function (data) {
            var validTbl = document.getElementById('validTable');
            var invalidTbl = document.getElementById('invalidTable');
            var invalidCount = 0;
            for (i = 0; i < data.length; i++) {
                if (data[i].valid == false) {
                    invalidCount++;
                }
            }
            if (invalidCount > 0) { //有非法信息
                invalidTbl.style.visibility = 'visible';
                validTbl.style.visibility = 'collapse';
                var index = 0;
                for (i = 0; i < data.length; i++) {
                    if (data[i].valid == true)
                        continue;

                    var tr = document.createElement('tr');
                    tr.style.textAlign = 'center';
                    var tr_index = document.createElement("th");
                    var tr_name = document.createElement("td");
                    var tr_id = document.createElement("td");
                    var tr_jb = document.createElement("td");
                    var tr_desc = document.createElement("td");
                    tr_desc.style.color = 'red';
                    tr_index.innerText = ++index;
                    tr_index.scope = 'row';
                    tr_name.innerText = data[i].name;
                    tr_id.innerText = data[i].id;
                    tr_jb.innerText = data[i].job;
                    tr_desc.innerText = data[i].dataDesc;
                    tr.appendChild(tr_index);
                    tr.appendChild(tr_name);
                    tr.appendChild(tr_id);
                    tr.appendChild(tr_jb);
                    tr.appendChild(tr_desc);
                    dt.appendChild(tr);
                }
            } else { //信息合法'
                invalidTbl.style.visibility = 'collapse';
                validTbl.style.visibility = 'visible';
                var validTblBody = document.getElementById('validInfo');
                validTblBody.innerHTML = '';
                var tr = document.createElement('tr');
                tr.style.textAlign = 'center';
                var tr_desc = document.createElement("td");
                var tr_count = document.createElement("td");
                var tr_price = document.createElement("td");
                tr_price.id = 'price';
                var tr_dateStart = document.createElement("td");
                tr_dateStart.id = "tr_startdate";
                var tr_dateEnd = document.createElement("td");
                tr_dateEnd.id = "tr_enddate";
                tr_desc.innerText = '数据无误';
                tr_desc.id = 'description';
                tr_desc.style.color = 'green';
                tr_count.innerText = data.length - invalidCount;
                tr_price.innerText = '请计算保费';
                tr_price.style.color = 'red';
                tr.appendChild(tr_desc);
                tr.appendChild(tr_count);
                tr.appendChild(tr_price);
                tr.appendChild(tr_dateStart);
                tr.appendChild(tr_dateEnd);
                validTblBody.appendChild(tr);
            }
        },
        fail: function (data) {
            alert(data);
        },
        error: function (data) {
            alert('错误');
        }
    });
}

function Calculate() {
    var fd = new FormData();
    if (document.getElementById('startdate').value == '') {
        alert("请选择生效日期");
        return;
    }
    fd.append('startDate', document.getElementById('startdate').value);
    $.ajax({
        type: 'post',
        url: '/EmployeeChange/CalculatePrice',
        async: true,
        data: fd,
        contentType: false,
        dataType: false,
        processData: false,
        success: function (data) {
            if (data == -9999999) {
                alert("生效日期不正确");
            } else if (data == -9999998) {
                alert("上传的文件中存在信息格式错误，修改后重试");
            } else if (data == -9999997) {
                alert("所选月份的表单不存在");
            } else if (data == -9999995) {
                alert("生效日期不正确，该月保单可能已被锁定");
            }
            else {
                document.getElementById('price').innerText = data;
            }
        },
        fail: function (data) {
            alert('fail');
        },
        error: function (data) {
            alert('error');
        }
    });
}

function AutoRenew(obj) {
    obj.disabled = true;
    obj.innerText = "请等待...";
    var comp = $(obj).attr('data-company')
    var d = $(obj).attr('data-enddate')
    var cm = $(obj).attr('data-currentmonth')
    $.ajax(
        {
            async: true,
            data: {
                CompanyName: comp,
                NextMonthEndDay: d,
                CurrentMonth: cm
            },
            type: 'post',
            url: '/Home/StartRenew',
            success: function (data) {
                alert(data);
                window.location.reload();
            }
        }
    );
}

function personDetail(detail) {
    var histories = detail.split('+');
    var body = document.getElementById('info');
    body.innerHTML = '';
    for (i = 0; i < histories.length; i++) {
        var detail = histories[i].split('%');
        var tr = document.createElement('tr');
        var td1 = document.createElement('td');
        var td2 = document.createElement('td');
        var td3 = document.createElement('td');
        var td4 = document.createElement('td');
        var td5 = document.createElement('td');
        var td6 = document.createElement('td');

        td1.innerText = detail[1];

        td2.innerText = detail[0];
        td3.innerText = detail[2];
        td4.innerText = detail[3];
        td5.innerText = detail[4];
        td6.innerText = detail[5];
        tr.appendChild(td1);
        tr.appendChild(td2);
        tr.appendChild(td3);
        tr.appendChild(td4);
        tr.appendChild(td5);
        tr.appendChild(td6);
        body.appendChild(tr);
    }
}


function showPersonalInfo(company, id) {
    $.ajax({
        type: 'get',
        async: true,
        url: '/Home/ShowPersonalDetail?comp=' + company + "&id=" + id,
        contentType: false,
        dataType: false,
        processData: false,
        success: function (data) {
            var body = document.getElementById('info');
            body.innerHTML = '';

        },
        fail: function (data) {
            alert('fail');
        },
        error: function (data) {
            alert('error');
        }
    });
}

function EncodeIEUrl(obj) {
    var re = new RegExp("[\\u4E00-\\u9FFF]+", "g");
    if (re.test(obj.href)) {
        var url = window.encodeURI(obj.href);
        obj.href = url;
    }
}
function showMsg() {
    var msg = document.getElementById('msg');
    if (msg.innerHTML != '') {
        alert(msg.innerHTML);
    }
}
function readFile() {

    if (prevFile.length > 9) {
        alert("图片数量上限为9个，请删除部分已选图片后重试");
        return;
    }
    var imgArea = document.getElementById("imgArea");
    imgArea.style.height = window.screen.height * 0.7 + 'px';
    imgArea.style.visibility = 'visible';
    var imgInput = document.getElementById("uploadImages");
    if (imgInput.files.length + prevFile.length > 9) {
        alert("图片数量上限为9个，请删除部分已选图片后重试");
        return;
    }
    var w = $('#imgArea').width();

    if (!CheckImageType(imgInput.files)) {
        alert('文件格式不支持，请上传图片文件')
        return;
    }
    filesCount = imgInput.files.length;
    btnSubmit.disabled = 'disabled';
    btnSubmit.style.backgroundColor = 'grey';
    setTimeout(function () {
        for (var i = 0; i < imgInput.files.length; i++) {
            var file = imgInput.files[i];
            if (isGifImg(file)) {
                prevFile.push(file); //无需压缩的图片添加进 prevFile链表中。
                CreateThumbnail(imgArea, file);
            }
            else {
                if (file.size > 1024 * 1024) {
                    CompressImages(file); //压缩后的图片在方法CompressImages中添加进 prevFile链表中。
                } else {
                    prevFile.push(file); //无需压缩的图片添加进 prevFile链表中。
                    CreateThumbnail(imgArea, file);
                }
            }
        }
        imgInput.value = '';

    }, 200);
}

function ShowLargeImage(image) {
    var imageIndex = image.parentNode.id;
    var Div_imgCircle = document.getElementById('imgCircle');
    Div_imgCircle.style.height = screenHeight * 0.82 + 'px';
    Div_imgCircle.style.width = $(Div_imgCircle).height() * 1.5 + 'px';
    var Div_imgCircle_ul = Div_imgCircle.getElementsByTagName('ul')[0];
    var div_carouselInner = document.getElementById('viewCarousel');

    for (i = 0; i < Div_imgCircle_ul.children.length; i++) {
        if (Div_imgCircle_ul.children[i].classList.contains("active")) {
            Div_imgCircle_ul.children[i].classList.remove("active");
            div_carouselInner.getElementsByClassName('imgBox')[i].parentNode.classList.remove("active");
            break;
        }
    }
    Div_imgCircle_ul.children[imageIndex].classList.add("active");
    div_carouselInner.getElementsByClassName('imgBox')[imageIndex].parentNode.classList.add("active");
    document.getElementById('imgCircle').style.visibility = 'visible';
    EV_modeAlert('imgCircle');
}

function ChangeHeight(obj) {
    obj.style.height = $(obj).width() + 'px';
}

function ReadyToPushFile(file) {
    if (file != null && file != undefined)
        prevFile.push(file);
}


//将base64转换为blob
function dataURLtoFile(dataurl, fileName) {
    var arr = dataurl.split(','),
        mime = arr[0].match(/:(.*?);/)[1],
        bstr = atob(arr[1]),
        n = bstr.length,
        u8arr = new Uint8Array(n);
    while (n--) {
        u8arr[n] = bstr.charCodeAt(n);
    }
    return new File([u8arr], fileName, { type: mime });
}


function getStyle(el, name) {
    if (window.getComputedStyle) {
        return window.getComputedStyle(el, null)[name];
    } else {
        return el.currentStyle[name];
    }
}
var compressedFile;

function CompressImages(file) {
    if (file.size <= 1024 * 1024) {
        return;
    }
    var ratio = file.size / 1024 / 1024;
    var img = document.createElement('img');
    var URL = window.URL || window.webkitURL || window.imgURL;
    //通过 file 生成目标 url
    var imgURL = URL.createObjectURL(file);
    var pa = document.getElementById('imgArea');
    img.src = imgURL;
    img.style.display = 'none';
    img.onload = function () { CompressImgToFile(img, ratio, compressedFile, CreateThumbnail); }
    pa.appendChild(img);
}

function CompressImgToFile(img, ratio, resultFile, callback) {
    var newCanvas = document.createElement("canvas");
    var ctx = newCanvas.getContext("2d");
    var sw = $(img).width();
    var sh = $(img).height();
    var mot;
    if (ratio > 2) {
        mot = ratio / 1.5;
    } else {
        mot = 1.3;
    }
    newCanvas.width = sw / mot;
    newCanvas.height = sh / mot;

    var dw = newCanvas.width;
    var dh = newCanvas.height;
    ctx.drawImage(img, /*0, 0, sw, sh,*/ 0, 0, dw, dh);
    img.outerHTML = '';
    var result = newCanvas.toDataURL('image/jpeg', 1);
    newCanvas = null;
    resultFile = dataURLtoFile(result, file.name);
    if (resultFile.size <= 1024 * 1024) {
        prevFile.push(resultFile);
        var imgArea = document.getElementById("imgArea");
        callback(imgArea, resultFile);
    } else {
        CompressImages(resultFile);
    }

}

function CheckImageType(obj) {
    var result = true;
    var img_id = '';
    for (var i = 0; i < obj.length; i++) {
        file = obj[i];
        var index = file.name.lastIndexOf(".");
        img_id = file.name.substring(index).toLowerCase(); //截断"."之前的，得到后缀
        if (img_id != ".bmp" && img_id != ".png" && img_id != ".gif" && img_id != ".jpg" && img_id != ".jpeg" && img_id != ".ico") {  //根据后缀，判断是否符合图片格式
            result = false;
            break;
        }
    }
    return result;
}

function CheckExcelType(obj) {
    var result = true;
    var img_id = '';
    for (var i = 0; i < obj.length; i++) {
        file = obj[i];
        var index = file.name.lastIndexOf(".");
        img_id = file.name.substring(index).toLowerCase();
        if (img_id != ".xls" && img_id != ".xlsx" && img_id != ".xlsm") {
            result = false;
            break;
        }
    }
    return result;
}

function isGifImg(gif) {
    var result = false;
    var img_id = '';
    var index = gif.name.lastIndexOf("."); //（考虑严谨用lastIndexOf(".")得到）得到"."在第几位
    img_id = gif.name.substring(index).toLowerCase(); //截断"."之前的，得到后缀
    if (img_id == ".gif") {  //根据后缀，判断是否符合图片格式
        result = true;
    }
    return result;
}

function InitializeSize() {
    document.getElementById('submitArea').style.width = $('main').width() * 0.99 + 'px';
    document.getElementsByName("storyContent")[0].innerHTML = document.getElementsByClassName("invisbleComment")[0].innerHTML;

}

function UpdateLiIndex(index) {
    var ulList = document.getElementById('imgList');
    for (var i = Number(index); i < ulList.children.length; i++) {
        if (ulList.children[i].id > index) {
            ulList.children[i].id -= 1;
        }
    }
}

function DetailImage(thisImg) {
    var base64Str = thisImg.src;
    thisImg.outerHTML = '';
    var parent = document.getElementById('imgArea');
    parent.style.height = window.screen.height * 0.7 + 'px';
    parent.style.visibility = 'visible';

    CreateThumbnail(parent, thisImg);
}
var imgRatio = 0.6;
var initialized = false;

function SetMargin(img) {
    if (!initialized) {
        initialized = true;
    }
    var par = img.parentNode.parentNode;
    //判断图片宽高类型
    if (img.width < img.height) {
        //图片高 > 宽
        //判断是竖屏还是横屏
        if (screenHeight > screenWidth) {
            //是竖屏，则设置图片高度为整个屏幕的95%，宽度auto
            img.style.height = screenHeight * 0.95 + 'px';
            img.style.width = 'auto';
            img.style.marginTop = '5px';
        } else {
            //横屏，则设置图片高度为整个弹出框高度的95%，弹出框高度已设置为屏幕高度的82%，宽度auto
            img.style.height = screenHeight * 0.82 * 0.95 + 'px';
            img.style.width = 'auto';
            img.style.marginTop = '5px';
        }
    } else {
        //图片宽 > 高
        //判断是竖屏还是横屏
        if (screenHeight > screenWidth) {
            //是竖屏，则设置图片宽度为整个屏幕的95%，高度auto
            img.style.width = screenWidth * 0.95 + 'px';
            img.style.height = 'auto';
            img.style.marginTop = '5px';
        } else {
            //横屏，则设置图片宽为屏幕高度的123%，高度auto
            img.style.width = screenHeight * 1.23 * 0.95 + 'px';
            img.style.height = 'auto';
            img.style.marginTop = screenHeight * 0.82 / 2 - img.height / 2;
        }
    }
    //img.style.marginLeft = ($(par).width() - $(img).width()) / 2 + 'px';
    //img.style.marginTop = ($(par).height() - $(img).height()) / 2 + 'px';
}

var CarouselImagesCreated = false;
function ShowCarousel(img) {
    if (CarouselImagesCreated) {
        return;
    }
    var d = document.createElement('imgCircle');
    d.style.height = screenHeight * 0.75 + 'px';
}


function CreateCarouselImage() {
    var div_carousel = document.getElementById('viewCarousel');
    for (i = 0; i < div_carousel.getElementsByTagName('div').length; i++) {
        var li = div_carousel.getElementsByTagName('div')[i];
        var imgBase64Str = li.innerText.trim();
        li.innerText = '';
        if (screenHeight > screenWidth) {
            li.style.width = '95%';
            li.style.height = $(li).width() * 0.5 + 'px';
        } else {
            li.style.width = '50%';
            li.style.height = $(li).width() * 0.7 + 'px';
        }
        var img = document.createElement('img');
        // 通过 file 生成目标 url
        var imgURL = imgBase64Str;
        img.src = imgURL;
        //img.style.visibility = 'hidden';
        img.align = 'center';
        img.onload = function () {
            if ($(img).width() < $(li).width() || $(img).height() < $(li).height()) {
                img.style.width = $(img).width() + 'px';
                img.style.height = $(img).height() + 'px';
            } else {
                var rawHeight = $(img).height();
                var rawWidth = $(img).width();
                img.style.height = '95%';
                var newHeight = $(img).height();
                img.style.width = rawWidth * newHeight / rawHeight + 'px';
            }
            var mar = Math.abs($(li).height() - $(img).height());
            img.style.marginTop = mar / 2 + 'px';
            img.style.visibility = 'visible';
        }
        li.appendChild(img);
        CarouselImagesCreated = true;
    }
}

function HomepageIntialize() {
    var hm_div = document.getElementsByClassName('carousel-item');
    var hm_img = document.getElementsByClassName('homePageImg');
    var hm_imgC = document.getElementById('imgCircle');
    var tb = document.getElementById('searchGroup');
    var dv = document.getElementsByClassName('searchElements')[0];
    //document.getElementById('News').style.height = $('#newsTable').height() + 300 + 'px';
    hm_imgC.style.margin = 'auto';
    var sc;
    var h = 0.3; //轮播图高度与屏幕高度比
    if (screenHeight < screenWidth) {
        for (i = 0; i < hm_div.length; i++) {
            dv.style.width = '80%';
            var shouldRemoveActive = false;
            if (!hm_div[i].classList.contains('active')) {
                hm_div[i].classList.add('active');
                if (i != 0)
                    shouldRemoveActive = true;
            }
            hm_div[i].style.height = h * screenHeight + 'px';
            sc_height = $(hm_div[i]).height() / $(hm_img[i]).height();
            sc_width = $(hm_div[i]).width() / $(hm_img[i]).width();
            sc = sc_height > sc_width ? sc_width : sc_height;//图片缩小后size与原size比，取宽高中较小的一个
            if (sc < 1) {
                hm_img[i].style.width = sc * $(hm_img[i]).width() + 'px';
            }
            //hTow = $(hm_img[i]).height() / $(hm_img[i]).width(); //图片宽高比
            //hm_img[i].style.height = sc * $(hm_div[i]).height() + 'px';
            //hm_img[i].style.width = 1 / hTow * $(hm_img[i]).height() + 'px';
            hm_img[i].style.visibility = 'visible';
            if (shouldRemoveActive) {
                hm_div[i].classList.remove('active');
            }
        }
    } else {

        tb.style.width = '95%';
        for (i = 0; i < hm_div.length; i++) {

            //添加active
            var shouldRemoveActive = false;
            if (!hm_div[i].classList.contains('active')) {
                hm_div[i].classList.add('active');
                if (i != 0)
                    shouldRemoveActive = true;
            }

            $(hm_div[i]).width('90%');
            $(hm_div[i]).height($(hm_div[i]).width() * 0.7);
            hm_div[i].style.margin = 'auto';
            sc_height = $(hm_div[i]).height() / $(hm_img[i]).height();
            sc_width = $(hm_div[i]).width() / $(hm_img[i]).width();
            sc = sc_height > sc_width ? sc_width : sc_height;
            if (sc < 1) {
                hm_img[i].style.width = $(hm_img[i]).width() * sc + 'px';
            }
            //hTow = $(hm_img[i]).height() / $(hm_img[i]).width();
            //$(hm_img[i]).width(sc * $(hm_img[i]).width() * 0.95);
            //$(hm_img[i]).height($(hm_img[i]).width() * hTow * 0.95);
            hm_img[i].style.marginTop = ($(hm_div[i]).height() - $(hm_img[i]).height()) / 2 + 'px';
            hm_img[i].style.visibility = 'visible';
            if (shouldRemoveActive) {
                hm_div[i].classList.remove('active');
            }
        }
    }
    hm_imgC.style.visibility = 'visible';
}

function CreateThumbnail(parent, file) {
    var URL = window.URL || window.webkitURL || window.imgURL;
    var li = document.createElement('li');
    if (screenHeight > screenWidth) {
        li.style.width = '45%';
        li.style.height = $('#imgArea').height() * 0.32 + 'px';
    } else {
        li.style.width = '32%';
        li.style.height = $('#imgArea').height() * 0.32 + 'px';
    }
    li.style.cssFloat = 'left';
    li.style.margin = '0.2% 0.667%'
    li.style.borderWidth = '3px';
    li.style.borderStyle = 'solid';
    li.style.textAlign = 'center';
    li.style.borderColor = 'grey';
    li.style.position = 'relative';
    var img = document.createElement('img');
    // 通过 file 生成目标 url
    if (file.src && file.src.substr(0, 4) == 'http') {
        img.src = file.src;
    } else {
        var imgURL = URL.createObjectURL(file);
        img.src = imgURL;
    }

    img.style.visibility = 'hidden';
    img.align = 'center';
    img.style.cursor = 'pointer';
    img.onclick = function () { ShowLargeImage(img); }
    img.onload = function () {
        if ($(img).width() < $(li).width() || $(img).height() < $(li).height()) {
            if ($(img).width() >= $(li).width() && $(img).height() < $(li).height()) {
                var originalW = $(img).width();
                var originalH = $(img).height();
                img.style.width = '95%';
                var changedW = $(img).width();
                var ratio = changedW / originalW;
                img.style.height = ratio * originalH + 'px';
            } else if ($(img).width() < $(li).width() && $(img).height() >= $(li).height()) {
                var originalW = $(img).width();
                var originalH = $(img).height();
                img.style.height = '95%';
                var changedH = $(img).height();
                var ratio = changedH / originalH;
                img.style.width = ratio * originalW + 'px';
            } else {

            }
        } else {
            var rawHeight = $(img).height();
            var rawWidth = $(img).width();
            var w_shrinkratio = $(li).width() / rawWidth;
            var h_shrinkratio = $(li).height() / rawHeight;

            var shrinkratio = w_shrinkratio > h_shrinkratio ? h_shrinkratio : w_shrinkratio;

            img.style.height = 0.95 * shrinkratio * rawHeight + 'px';
            //var newHeight = $(img).height();
            img.style.width = 'auto';
        }
        var mar = Math.abs($(li).height() - $(img).height());
        img.style.marginTop = mar / 2 + 'px';
        img.style.visibility = 'visible';
    }
    if (typeof (filesCount) != "undefined") {
        li.onmouseenter = function () { CreateDeleteBtn(li); }
        li.onmouseleave = function () { RemoveDeleteBtn(li); }
    }
    li.appendChild(img);
    li.id = currentLiIndex;
    parent.getElementsByTagName('ul')[0].appendChild(li);
    currentLiIndex += 1;
    filesCompressed += 1;
    if (typeof (filesCount) != "undefined") {
        if (filesCompressed == filesCount) {
            btnSubmit.disabled = '';
            btnSubmit.style.backgroundColor = 'orange';
            filesCompressed = 0;
        }
    }
}

////使用Canvas实现缩略图，由于无法在Canvas上添加其他元素等问题，暂时废弃该部分代码
//function GetBase64String(img, file) {
//    if (file.size <= 1024 * 1024) {
//        return;
//    }
//    var newCanvas = document.createElement("canvas");
//    var ctx = newCanvas.getContext("2d");
//    var sw = $(img).width();
//    var sh = $(img).height();

//    newCanvas.style.width = sw / 5 + 'px';
//    newCanvas.style.height = sh / 5 + 'px';

//    var dw = $(newCanvas).width();
//    var dh = $(newCanvas).height();
//    ctx.drawImage(img, 0, 0, sw, sh, 0, 0, dw, dh);
//    var result = newCanvas.toDataURL('image/jpeg', 1);
//    newCanvas = null;
//    return result;
//}

function showLargeImg(obj) {

}

function CreateDeleteBtn(obj) {
    var delBtn = document.createElement('div');
    delBtn.style.width = $(obj).width() + 'px';
    delBtn.style.height = 0.1 * $(obj).height() + 'px';
    delBtn.style.backgroundColor = 'red';
    delBtn.style.textAlign = 'center';
    delBtn.innerText = '删除';
    delBtn.style.margin = '0';
    delBtn.style.position = 'absolute';
    delBtn.style.top = '5px';
    //delBtn.style.position = 'absolute';
    delBtn.onclick = function () { DeleteThumbnail(obj) };
    obj.appendChild(delBtn);
}

function DeleteThumbnail(obj) {
    obj.outerHTML = '';
    UpdateLiIndex(obj.id);
    prevFile.splice(obj.id, 1);
    currentLiIndex -= 1;

}
function RemoveDeleteBtn(obj) {
    var btn = obj.getElementsByTagName('div')[0];
    btn.outerHTML = '';
}

function toUtf8(str) {
    var out, i, len, c;
    out = "";
    len = str.length;
    for (i = 0; i < len; i++) {
        c = str.charCodeAt(i);
        if ((c >= 0x0001) && (c <= 0x007F)) {
            out += str.charAt(i);
        } else if (c > 0x07FF) {
            out += String.fromCharCode(0xE0 | ((c >> 12) & 0x0F));
            out += String.fromCharCode(0x80 | ((c >> 6) & 0x3F));
            out += String.fromCharCode(0x80 | ((c >> 0) & 0x3F));
        } else {
            out += String.fromCharCode(0xC0 | ((c >> 6) & 0x1F));
            out += String.fromCharCode(0x80 | ((c >> 0) & 0x3F));
        }
    }
    return out;
}

function ValidateAndUpload() {
    cansubmit = false;
    imgs_failed_upload = 0;
    var result = true;
    var goon = true;



    var gameName = document.getElementById("GameName").value;
    gameName = toUtf8(gameName);

    var idrole = $('#IdRole').val();
    var StoryTitle = $('#StoryTitle').val();
    var ServerName = $('#ServerName').val();
    var storyContent = divComment.innerText;
    var storyTime = document.getElementsByName('storyDate')[0].value;
    var model = {
        IdRole: idrole,
        ServerName: ServerName,
        StoryContent: storyContent,
        GameName: gameName,
        StoryTitle: StoryTitle,
        StoryTime: storyTime,
    }
    var model1 = JSON.stringify(model);
    $.ajax({
        type: 'post',
        url: '/Submit/ValidateModel',
        data: model1,
        dataType: 'json',
        async: true,
        contentType: 'application/json',
        success: function (data) {
            if (data == true) {
                var formData = new FormData();
                for (var i = 0; i < prevFile.length; i++) {
                    formData.append('imageFiles' + i, prevFile[i]);
                }
                $.ajax({
                    type: 'Post',
                    url: '/Submit/ValidateImagesFromJS',
                    cache: false,
                    async: true,
                    data: formData,
                    contentType: false,//必须false才会自动加上正确的Content-Type
                    processData: false,
                    beforeSend: function (xhr) {
                        xhr.setRequestHeader("Game", gameName);
                    },
                    success: function (data) {
                        if (data != "" && data != "-1") { //若图片数量 > 0，则进行上传
                            document.getElementById("guid").value = data;
                            data = folder + data;
                            for (i = 0; i < prevFile.length; i++) {
                                var file = prevFile[i];
                                uploadFile(file, data, function (err, data) {
                                    if (err) {
                                        result = false;
                                        i = prevFile.length;
                                    }
                                });
                            }
                        } else {//若没有图片需要上传，则直接请求成功页面

                        }
                    },
                    fail: function (data) {
                        alert("图片格式有误,请检查后再试");
                        EV_closeAlert("waiting");
                        result = false;
                    },
                    error: function (err) {
                        alert("图片格式有误,请检查后再试");
                        EV_closeAlert("waiting");
                        result = false;
                    }
                });
            } else {
                goon = false;
                result = false;
            }
        },
        fail: function (data) {
            result = false;
            goon = false;
            model_invalid();
        },
        error: function (err) {
            result = false;
            goon = false;
            model_invalid();
        }
    });

    //if (result == false) {
    //    alert("格式有误,请检查后再试");
    //    //btnSubmit.disabled = 'true';
    //    EV_closeAlert("waiting");
    //}
    //return result;
}

function model_invalid() {
    alert("格式有误,请检查后再试");
    EV_closeAlert("waiting");
}

var cansubmit;
var imgs_failed_upload = 0;

function ShowProgressBar() {
    var progressBar = document.getElementById('waiting');
    progressBar.style.visibility = 'visible';
    EV_modeAlert("waiting");
}
var divComment, inputComment;
function UpdateComment() {
    divComment = document.getElementsByClassName("comment")[0];
    inputComment = document.getElementsByClassName("invisbleComment")[0];
    inputComment.value = divComment.innerHTML;

    setTimeout(ValidateAndUpload, 100);
    ShowProgressBar();
}

function wol() {
    var mydateInput = document.getElementsByName("storyDate")[0]
    var date = new Date();
    var month = date.getMonth();
    if (month.toString().length == 1) {
        month = "0" + month;
    }
    var day = date.getDate();
    if (day.toString().length == 1) {
        day = "0" + day;
    }
    var dateString = date.getFullYear() + "/" + month + "/" + day;
    mydateInput.value = dateString;
}

function isValidSubmit() {
    if (canRegister) {
        return true;
    } else {
        return false;
    }
}

function isValidReset() {
    return canReset;
}

var k;
function Encrypt() {
    var pwdBox = document.getElementById("pwdBox");
    var confirmPwd = document.getElementById("confirmPwd");
    k = getCookie("invalidFlag");
    if (pwdBox == "" || confirmPwd == "") {
        canRegister = false;
        canReset = false;
        return;
    }
    var water = getCookie("Frontend")
    for (i = 1; i <= 500; i++) {
        //pwdBox.value = md(pwdBox.value + water);
        //confirmPwd.value = md(confirmPwd.value + water);
    }
    canRegister = true;
    canReset = true;
}


var Bucket = 'vgamestory-1300222705';
var Region = 'ap-shanghai';
var protocol = location.protocol === 'https:' ? 'https:' : 'http:';
var prefix = protocol + '//' + Bucket + '.cos.' + Region + '.myqcloud.com/';
var cos;
var credentials;
// 初始化实例
function InitializeCOS() {
    cos = new COS({
        getAuthorization: getAuthorization({ Method: 'PUT', Pathname: '/' + Key }, Test)
    });
}

// 计算签名
var getAuthorization = function (options, callback) {
    var url = '/TencentCOS/GetWriteOnlyCredential';
    var xhr = new XMLHttpRequest();
    xhr.open('GET', url, false);
    xhr.onreadystatechange = function (e) {
        if (xhr.readyState == 4 && xhr.status == 200) {
            var credentials;
            credentials = eval("(" + xhr.response + ")");
            credentials = credentials.value;
            if (credentials) {
                callback(null, {
                    XCosSecurityToken: credentials.Credentials.Token,
                    Authorization: CosAuth({
                        SecretId: credentials.Credentials.TmpSecretId,
                        SecretKey: credentials.Credentials.TmpSecretKey,
                        Method: options.Method,
                        Pathname: options.Pathname,
                        ExpiredTime: credentials.ExpiredTime
                    })
                });
            } else {
                callback('获取签名出错1');
            }
        }
    };
    xhr.onerror = function (e) {
        callback('获取签名出错2');
    };
    xhr.send();
};

//function getAuthorization(options, callback) {
//    // 异步获取临时密钥
//    $.get('/TencentCOS/GetTempCredential', {
//        bucket: _bucket,
//        region: _region,
//    }, function (data) {
//        credentials = eval("(" + data.value + ")");
//        callback(null, {
//            XCosSecurityToken: credentials.sessionToken,
//            Authorization: CosAuth({
//                SecretId: credentials.tmpSecretId,
//                SecretKey: credentials.tmpSecretKey,
//                Method: options.Method,
//                Pathname: options.Pathname,
//            })
//        });
//    });
//}

//function UploadImgToCOS(files, path) {
//    var result = true;
//    for (i = 0; i < files.length; i++) {
//        cos.putObject({
//            Bucket: _bucket,
//            Region: _region,
//            Key: path + files[i].fileName,
//            Body: files[i],
//        }, function (err, data) {
//            console.log(err || data);
//        });
//    }
//    return result;
//}

function UpdateValidationImg(url) {
    var xhr = new XMLHttpRequest();
    xhr.open("Get", url, false);
    xhr.onreadystatechange = function () {
        if (xhr.readyState == 4 && xhr.status == 200) {
            document.getElementById('validationImg').src = xhr.responseText;
        }
    }
    xhr.send();
}

function async_ValidateMail(callbk_validateFields) {
    var hr = new XMLHttpRequest();
    var img_loading_mail = document.getElementById('img_loading_mail');
    var inputMail = document.getElementById('MailAddress').value;
    if (inputMail == "") {
        callbk_validateFields();
        document.getElementById('mailMsg').style.visibility = 'hidden';
        return;
    }
    hr.open("GET", "MailExists?input=" + inputMail, true);
    hr.onreadystatechange = function () {
        if (hr.readyState == 4 && hr.status == 200) {
            var result = hr.responseText;
            if (result == "true") {
                mailPass = false;
                img_loading_mail.style.visibility = 'hidden';
                document.getElementById('mailMsg').style.color = 'red';
                document.getElementById('mailMsg').innerText = '该邮箱已被注册';
                document.getElementById('mailMsg').style.visibility = 'visible';
                callbk_validateFields();
            } else if (result == "false") {
                mailPass = true;
                img_loading_mail.style.visibility = 'hidden';
                document.getElementById('mailMsg').innerText = '√ 该邮箱可用';
                document.getElementById('mailMsg').style.visibility = 'visible';
                document.getElementById('mailMsg').style.color = 'green';
                callbk_validateFields();
            }
        }
    }
    img_loading_mail.style.visibility = 'visible';
    hr.send();
}


function rstpwd_validateFields() {
    var id_box = document.getElementById('id');
    var mail_box = document.getElementById('mail');
    var canSubmit = true;
    if (id_box.value == '' || mail_box.value == '') {
        canSubmit = false;
    }

}


function async_ValidateResetPwd() {
    var hr = new XMLHttpRequest();
    var inputRes = document.getElementById('validationResult').value;
    if (inputRes == "") {
        document.getElementById('validationMsg').style.visibility = 'hidden';
        return;
    }
    if (parseInt(inputRes).toString() == "NaN") {
        document.getElementById('validationMsg').innerText = '请输入数字';
        document.getElementById('validationMsg').style.color = 'red';
        document.getElementById('validationMsg').style.visibility = 'visible';
        return;
    }
    hr.open("GET", "/Users/ValidationResult?input=" + inputRes, true);
    hr.onreadystatechange = function () {
        if (hr.readyState == 4 && hr.status == 200) {
            var result = hr.responseText;
            if (result == "false") {
                validationPass = false;
                document.getElementById('validationResult').value = '';
                document.getElementById('validationMsg').style.color = 'red';
                document.getElementById('validationMsg').innerText = '× ';
                document.getElementById('validationMsg').style.visibility = 'visible';
                UpdateValidationImg("Users/UpdateValidationImg");
            } else if (result == "true") {
                validationPass = true;
                document.getElementById('validationMsg').innerText = '√ ';
                document.getElementById('validationMsg').style.visibility = 'visible';
                document.getElementById('validationMsg').style.color = 'green';
            }
        }
    }
    hr.send();
}

function async_Validate(callbk_validateFields) {
    var img_loading_validaitoncode = document.getElementById('img_loading_validationcode');
    var hr = new XMLHttpRequest();
    var inputRes = document.getElementById('validationResult').value;
    if (inputRes == "") {
        callbk_validateFields();
        document.getElementById('validationMsg').style.visibility = 'hidden';
        return;
    }
    if (parseInt(inputRes).toString() == "NaN") {
        document.getElementById('validationMsg').innerText = '请输入数字';
        document.getElementById('validationMsg').style.color = 'red';
        document.getElementById('validationMsg').style.visibility = 'visible';
        return;
    }
    hr.open("GET", "/Login/ValidationResult?input=" + inputRes, true);
    hr.onreadystatechange = function () {
        if (hr.readyState == 4 && hr.status == 200) {
            var result = hr.responseText;
            if (result == "false") {
                validationPass = false;
                img_loading_validaitoncode.style.visibility = 'hidden';
                document.getElementById('validationResult').value = '';
                document.getElementById('validationMsg').style.color = 'red';
                document.getElementById('validationMsg').innerText = '×';
                document.getElementById('validationMsg').style.visibility = 'visible';
                callbk_validateFields();
                UpdateValidationImg("/Login/UpdateValidationImg");
            } else if (result == "true") {
                validationPass = true;
                img_loading_validaitoncode.style.visibility = 'hidden';
                document.getElementById('validationMsg').innerText = '√';
                document.getElementById('validationMsg').style.visibility = 'visible';
                document.getElementById('validationMsg').style.color = 'green';
                callbk_validateFields();
            }
        }
    }
    img_loading_validaitoncode.style.visibility = 'visible';
    hr.send();
}
var validationPass = false;
var userNamePass = false;
var pwdPass = false;
var canRegister = false;
var canReset = false;
var mailPass = false;
var isAgreePass = false;
var id_pattern = /^[a-zA-Z\u4E00-\u9FA5]{1}[a-zA-Z0-9_\u4E00-\u9FA5]{1,17}$/;
//var mail_pattern = /^[\.a-zA-Z0-9_-]+@[a-zA-Z0-9_-]+(\.[a-zA-Z0-9_-]+)+$/;
var mail_pattern = /[\w!#$%&'*+/=?^_`{|}~-]+(?:\.[\w!#$%&'*+/=?^_`{|}~-]+)*@(?:[\w](?:[\w-]*[\w])?\.)+[\w](?:[\w-]*[\w])?/;
var pwd_pattern = /^(?=.*\d)(?=.*[a-zA-Z])[0-9a-zA-Z_]{6,20}$/;
var valiCode_pattern = /^-?[1-9]\d*$/;
var paidmoney_pattern = /^\d+(\.\d+)?$/;　　 //匹配非负浮点数（正浮点数 + 0）
var paidmoney_pattern2 = /^\d+$/;　　 ////匹配非负整数（正整数 + 0）

function ValidateFields_RegisterView() {
    var registerPageInputResult = true;
    var pwdBox = document.getElementById("pwdBox");
    var idBox = document.getElementById("UserName");
    var cPwdBox = document.getElementById('confirmPwd');
    if (idBox.value == "" || userNamePass == false || !id_pattern.test(idBox.value)) {
        registerPageInputResult = false;
    }

    if (pwdBox.value == "" || pwdPass == false) {
        registerPageInputResult = false;
    }

    if (cPwdBox.value == "" || pwdPass == false) {
        registerPageInputResult = false;
    }

    var val = document.getElementById('validationResult');
    if (val.value == "" || validationPass == false || !valiCode_pattern.test(val.value)) {
        registerPageInputResult = false;
    }

    if (registerPageInputResult) {
        document.getElementById('btnRegister').disabled = '';
        canRegister = true;
    } else {
        document.getElementById('btnRegister').disabled = 'disabled';
        canRegister = false;
    }
}

function UserNameExists(userName) {
    ValidateFields_RegisterView();
    var idBox = document.getElementById("UserName");
    if (idBox.value == "") {
        document.getElementById('userExists').innerText = '';
        return;
    }

    if (!id_pattern.test(idBox.value)) {
        document.getElementById('userExists').innerText = 'X 用户名必须以汉字或字母开头，可包含汉字、字母、数字及下划线，最短为2个字符，最长为18个字符';
        document.getElementById('userExists').style.color = "red";
        return;
    }
    var img_loading_user = document.getElementById("img_loading_username");
    var xhr = new XMLHttpRequest();
    xhr.open("Get", "UserNameExists?userName=" + userName, true);
    xhr.onreadystatechange = function () {
        if (xhr.readyState == 4 && xhr.status == 200) {
            if (xhr.responseText == "true") {
                userNamePass = false;
                img_loading_user.style.visibility = 'hidden';
                document.getElementById('userExists').innerText = 'X 用户名已存在';
                document.getElementById('userExists').style.color = "red";
                ValidateFields_RegisterView();
            } else {
                userNamePass = true;
                img_loading_user.style.visibility = 'hidden';
                document.getElementById('userExists').innerText = '√ 用户名可用';
                document.getElementById('userExists').style.color = "green";
                ValidateFields_RegisterView();
            }
        }
    }
    img_loading_user.style.visibility = 'visible';
    xhr.send();
}

function PwdValid() {
    var pwdBox = document.getElementById("pwdBox");
    var cPwdBox = document.getElementById('confirmPwd');

    if (pwdBox.value == "" || cPwdBox.value == "" || pwdBox.value != cPwdBox.value) {
        pwdPass = false;
        if (pwdBox.value != cPwdBox.value && pwdBox.value != "" && cPwdBox.value != "") {
            document.getElementById("cPwdMsg").innerText = "两次输入的密码不一致";
            document.getElementById("cPwdMsg").style.visibility = "visible";
        }
    } else if (!pwd_pattern.test(pwdBox.value)) {
        pwdPass = false;
        document.getElementById("cPwdMsg").innerText = "密码必须包含字母及数字，可包含字母、数字及下划线，长度为7-20位。";
        document.getElementById("cPwdMsg").style.visibility = "visible";
    }
    else {
        document.getElementById("cPwdMsg").innerText = "√ 密码可用";
        document.getElementById("cPwdMsg").style.color = "green";
        pwdPass = true;
    }

    ValidateFields_RegisterView();
}

function ValidateFields_ResetPwdView() {
    var pwdBox = document.getElementById("pwdBox");
    var cPwdBox = document.getElementById("confirmPwd");
    var idBox = document.getElementById("UserName");
    var validationBox = document.getElementById("validationResult");
    var errorMsg = document.getElementById("pwdErr");
    if (pwdBox.value != "" && idBox.value != "" && validationBox.value != "" && cPwdBox.value != "" && pwdBox.value == cPwdBox.value && pwd_pattern.test(pwdBox.value)) {
        document.getElementById('btnSubmit').disabled = '';
        errorMsg.innerText = "";
        canRegister = true;
    } else {
        if (pwdBox.value != cPwdBox.value) {
            errorMsg.innerText = "两次输入的密码不一致";
        } else if (!pwd_pattern.test(pwdBox.value)) {
            errorMsg.innerText = "密码必须包含字母及数字，可包含字母、数字及下划线，长度为7-20位。";
        }
        document.getElementById('btnSubmit').disabled = 'disabled';
        canRegister = false;
    }
}

function ValidateFields_LoginView() {
    var pwdBox = document.getElementById("pwdBox");
    var idBox = document.getElementById("UserName");
    var validationBox = document.getElementById("validationResult");
    if (pwdBox.value != "" && idBox.value != "" && validationBox.value != "") {
        document.getElementById('btnSubmit').disabled = '';
    } else {
        document.getElementById('btnSubmit').disabled = 'disabled';
    }
}

function LoginValidate() {
    //document.getElementById('btnSubmit').style.background = 'rgb(213,162,184)';
    var pwdBox = document.getElementById("pwdBox");
    var water = getCookie("Frontend");
    //alert(water);
    //alert(getCookie("MD5Iter"));
    //for (i = 1; i <= getCookie("MD5Iter"); i++) {
    //    pwdBox.value = md(pwdBox.value + water);
    //}
    //alert(pwdBox.value);

}

function ResetPwd() {
    var pwdBox = document.getElementById("pwdBox");
    pwdBox.value = "";
    var conPwd = document.getElementById("confirmPwd");
    if (conPwd != null) {
        conPwd.value = "";
    }
    var idBox = document.getElementById("UserName");
    if (idBox)
        idBox.value = "";
}

function InitializeRegisterPage() {
    SetFieldsEvent();
}

function ShowPwsTips() {
    var msg = document.getElementById("cPwdMsg");
    msg.innerText = "密码须包含字母及数字，可由字母、数字及下划线构成，长度为7-20位。";
}
var fieldsList_register = new Array();
function SetFieldsEvent() {
    //为各输入框绑定onblur事件
    var pwdBox = document.getElementById("pwdBox");
    var idBox = document.getElementById("UserName");
    var cPwdBox = document.getElementById('confirmPwd');
    cPwdBox.onfocus = function () {
        ShowPwsTips();
    }
    pwdBox.onfocus = function () {
        ShowPwsTips();
    }
    idBox.onblur = function () { UserNameExists(idBox.value) };
    pwdBox.onblur = function () { PwdValid() };
    cPwdBox.onblur = function () { PwdValid() };
}

function IsAgreementChecked() {
    var cb = document.getElementById('isAgree');
    if (cb.checked == true) {
        isAgreePass = true;
    } else {
        isAgreePass = false;
    }
    ValidateFields_RegisterView();
}

function MailValdiation(mailBox) {
    var v = document.getElementById('mailMsg');
    if (mailBox.value == '') {
        v.innerText = '';
        return;
    }
    if (!mail_pattern.test(mailBox.value)) {
        v.innerText = '邮箱格式不正确';
        v.style.color = 'red';
        ValidateFields_RegisterView();
        return;
    } else {
        v.innerText = '';
    }
    async_ValidateMail(ValidateFields_RegisterView);
}

function getCookie(c_name) {
    if (document.cookie.length > 0) {
        c_start = document.cookie.indexOf(c_name + "=")
        if (c_start != -1) {
            c_start = c_start + c_name.length + 1
            c_end = document.cookie.indexOf(";", c_start)
            if (c_end == -1) c_end = document.cookie.length
            return unescape(document.cookie.substring(c_start, c_end))
        }
    }
    return ""
}

var rotateLeft = function (lValue, iShiftBits) {
    return (lValue << iShiftBits) | (lValue >>> (32 - iShiftBits));
}
var addUnsigned = function (lX, lY) {
    var lX4, lY4, lX8, lY8, lResult;
    lX8 = (lX & 0x80000000);
    lY8 = (lY & 0x80000000);
    lX4 = (lX & 0x40000000);
    lY4 = (lY & 0x40000000);
    lResult = (lX & 0x3FFFFFFF) + (lY & 0x3FFFFFFF);
    if (lX4 & lY4) return (lResult ^ 0x80000000 ^ lX8 ^ lY8);
    if (lX4 | lY4) {
        if (lResult & 0x40000000) return (lResult ^ 0xC0000000 ^ lX8 ^ lY8);
        else return (lResult ^ 0x40000000 ^ lX8 ^ lY8);
    } else {
        return (lResult ^ lX8 ^ lY8);
    }
}
var F = function (x, y, z) {
    return (x & y) | ((~x) & z);
}
var G = function (x, y, z) {
    return (x & z) | (y & (~z));
}
var H = function (x, y, z) {
    return (x ^ y ^ z);
}
var I = function (x, y, z) {
    return (y ^ (x | (~z)));
}
var FF = function (a, b, c, d, x, s, ac) {
    a = addUnsigned(a, addUnsigned(addUnsigned(F(b, c, d), x), ac));
    return addUnsigned(rotateLeft(a, s), b);
};
var GG = function (a, b, c, d, x, s, ac) {
    a = addUnsigned(a, addUnsigned(addUnsigned(G(b, c, d), x), ac));
    return addUnsigned(rotateLeft(a, s), b);
};
var HH = function (a, b, c, d, x, s, ac) {
    a = addUnsigned(a, addUnsigned(addUnsigned(H(b, c, d), x), ac));
    return addUnsigned(rotateLeft(a, s), b);
};
var II = function (a, b, c, d, x, s, ac) {
    a = addUnsigned(a, addUnsigned(addUnsigned(I(b, c, d), x), ac));
    return addUnsigned(rotateLeft(a, s), b);
};
var convertToWordArray = function (string) {
    var lWordCount;
    var lMessageLength = string.length;
    var lNumberOfWordsTempOne = lMessageLength + 8;
    var lNumberOfWordsTempTwo = (lNumberOfWordsTempOne - (lNumberOfWordsTempOne % 64)) / 64;
    var lNumberOfWords = (lNumberOfWordsTempTwo + 1) * 16;
    var lWordArray = Array(lNumberOfWords - 1);
    var lBytePosition = 0;
    var lByteCount = 0;
    while (lByteCount < lMessageLength) {
        lWordCount = (lByteCount - (lByteCount % 4)) / 4;
        lBytePosition = (lByteCount % 4) * 8;
        lWordArray[lWordCount] = (lWordArray[lWordCount] | (string.charCodeAt(lByteCount) << lBytePosition));
        lByteCount++;
    }
    lWordCount = (lByteCount - (lByteCount % 4)) / 4;
    lBytePosition = (lByteCount % 4) * 8;
    lWordArray[lWordCount] = lWordArray[lWordCount] | (0x80 << lBytePosition);
    lWordArray[lNumberOfWords - 2] = lMessageLength << 3;
    lWordArray[lNumberOfWords - 1] = lMessageLength >>> 29;
    return lWordArray;
};
var wordToHex = function (lValue) {
    var WordToHexValue = "", WordToHexValueTemp = "", lByte, lCount;
    for (lCount = 0; lCount <= 3; lCount++) {
        lByte = (lValue >>> (lCount * 8)) & 255;
        WordToHexValueTemp = "0" + lByte.toString(16);
        WordToHexValue = WordToHexValue + WordToHexValueTemp.substr(WordToHexValueTemp.length - 2, 2);
    }
    return WordToHexValue;
};
var uTF8Encode = function (string) {
    string = string.replace(/\x0d\x0a/g, "\x0a");
    var output = "";
    for (var n = 0; n < string.length; n++) {
        var c = string.charCodeAt(n);
        if (c < 128) {
            output += String.fromCharCode(c);
        } else if ((c > 127) && (c < 2048)) {
            output += String.fromCharCode((c >> 6) | 192);
            output += String.fromCharCode((c & 63) | 128);
        } else {
            output += String.fromCharCode((c >> 12) | 224);
            output += String.fromCharCode(((c >> 6) & 63) | 128);
            output += String.fromCharCode((c & 63) | 128);
        }
    }
    return output;
};

function md(string) {
    var x = Array();
    var k, AA, BB, CC, DD, a, b, c, d;
    var S11 = 7, S12 = 12, S13 = 17, S14 = 22;
    var S21 = 5, S22 = 9, S23 = 14, S24 = 20;
    var S31 = 4, S32 = 11, S33 = 16, S34 = 23;
    var S41 = 6, S42 = 10, S43 = 15, S44 = 21;
    string = uTF8Encode(string);
    x = convertToWordArray(string);
    a = 0x67452301; b = 0xEFCDAB89; c = 0x98BADCFE; d = 0x10325476;
    for (k = 0; k < x.length; k += 16) {
        AA = a; BB = b; CC = c; DD = d;
        a = FF(a, b, c, d, x[k + 0], S11, 0xD76AA478);
        d = FF(d, a, b, c, x[k + 1], S12, 0xE8C7B756);
        c = FF(c, d, a, b, x[k + 2], S13, 0x242070DB);
        b = FF(b, c, d, a, x[k + 3], S14, 0xC1BDCEEE);
        a = FF(a, b, c, d, x[k + 4], S11, 0xF57C0FAF);
        d = FF(d, a, b, c, x[k + 5], S12, 0x4787C62A);
        c = FF(c, d, a, b, x[k + 6], S13, 0xA8304613);
        b = FF(b, c, d, a, x[k + 7], S14, 0xFD469501);
        a = FF(a, b, c, d, x[k + 8], S11, 0x698098D8);
        d = FF(d, a, b, c, x[k + 9], S12, 0x8B44F7AF);
        c = FF(c, d, a, b, x[k + 10], S13, 0xFFFF5BB1);
        b = FF(b, c, d, a, x[k + 11], S14, 0x895CD7BE);
        a = FF(a, b, c, d, x[k + 12], S11, 0x6B901122);
        d = FF(d, a, b, c, x[k + 13], S12, 0xFD987193);
        c = FF(c, d, a, b, x[k + 14], S13, 0xA679438E);
        b = FF(b, c, d, a, x[k + 15], S14, 0x49B40821);
        a = GG(a, b, c, d, x[k + 1], S21, 0xF61E2562);
        d = GG(d, a, b, c, x[k + 6], S22, 0xC040B340);
        c = GG(c, d, a, b, x[k + 11], S23, 0x265E5A51);
        b = GG(b, c, d, a, x[k + 0], S24, 0xE9B6C7AA);
        a = GG(a, b, c, d, x[k + 5], S21, 0xD62F105D);
        d = GG(d, a, b, c, x[k + 10], S22, 0x2441453);
        c = GG(c, d, a, b, x[k + 15], S23, 0xD8A1E681);
        b = GG(b, c, d, a, x[k + 4], S24, 0xE7D3FBC8);
        a = GG(a, b, c, d, x[k + 9], S21, 0x21E1CDE6);
        d = GG(d, a, b, c, x[k + 14], S22, 0xC33707D6);
        c = GG(c, d, a, b, x[k + 3], S23, 0xF4D50D87);
        b = GG(b, c, d, a, x[k + 8], S24, 0x455A14ED);
        a = GG(a, b, c, d, x[k + 13], S21, 0xA9E3E905);
        d = GG(d, a, b, c, x[k + 2], S22, 0xFCEFA3F8);
        c = GG(c, d, a, b, x[k + 7], S23, 0x676F02D9);
        b = GG(b, c, d, a, x[k + 12], S24, 0x8D2A4C8A);
        a = HH(a, b, c, d, x[k + 5], S31, 0xFFFA3942);
        d = HH(d, a, b, c, x[k + 8], S32, 0x8771F681);
        c = HH(c, d, a, b, x[k + 11], S33, 0x6D9D6122);
        b = HH(b, c, d, a, x[k + 14], S34, 0xFDE5380C);
        a = HH(a, b, c, d, x[k + 1], S31, 0xA4BEEA44);
        d = HH(d, a, b, c, x[k + 4], S32, 0x4BDECFA9);
        c = HH(c, d, a, b, x[k + 7], S33, 0xF6BB4B60);
        b = HH(b, c, d, a, x[k + 10], S34, 0xBEBFBC70);
        a = HH(a, b, c, d, x[k + 13], S31, 0x289B7EC6);
        d = HH(d, a, b, c, x[k + 0], S32, 0xEAA127FA);
        c = HH(c, d, a, b, x[k + 3], S33, 0xD4EF3085);
        b = HH(b, c, d, a, x[k + 6], S34, 0x4881D05);
        a = HH(a, b, c, d, x[k + 9], S31, 0xD9D4D039);
        d = HH(d, a, b, c, x[k + 12], S32, 0xE6DB99E5);
        c = HH(c, d, a, b, x[k + 15], S33, 0x1FA27CF8);
        b = HH(b, c, d, a, x[k + 2], S34, 0xC4AC5665);
        a = II(a, b, c, d, x[k + 0], S41, 0xF4292244);
        d = II(d, a, b, c, x[k + 7], S42, 0x432AFF97);
        c = II(c, d, a, b, x[k + 14], S43, 0xAB9423A7);
        b = II(b, c, d, a, x[k + 5], S44, 0xFC93A039);
        a = II(a, b, c, d, x[k + 12], S41, 0x655B59C3);
        d = II(d, a, b, c, x[k + 3], S42, 0x8F0CCC92);
        c = II(c, d, a, b, x[k + 10], S43, 0xFFEFF47D);
        b = II(b, c, d, a, x[k + 1], S44, 0x85845DD1);
        a = II(a, b, c, d, x[k + 8], S41, 0x6FA87E4F);
        d = II(d, a, b, c, x[k + 15], S42, 0xFE2CE6E0);
        c = II(c, d, a, b, x[k + 6], S43, 0xA3014314);
        b = II(b, c, d, a, x[k + 13], S44, 0x4E0811A1);
        a = II(a, b, c, d, x[k + 4], S41, 0xF7537E82);
        d = II(d, a, b, c, x[k + 11], S42, 0xBD3AF235);
        c = II(c, d, a, b, x[k + 2], S43, 0x2AD7D2BB);
        b = II(b, c, d, a, x[k + 9], S44, 0xEB86D391);
        a = addUnsigned(a, AA);
        b = addUnsigned(b, BB);
        c = addUnsigned(c, CC);
        d = addUnsigned(d, DD);
    }
    var tempValue = wordToHex(a) + wordToHex(b) + wordToHex(c) + wordToHex(d);
    return tempValue.toUpperCase();
}


//页面蒙版
//用来记录要显示的DIV的ID值
var EV_MsgBox_ID = "";
//重要    
//弹出对话窗口(msgID-要显示的div的id) 
function EV_modeAlert(msgID) {
    //创建大大的背景框   
    var bgObj = document.createElement("div");
    bgObj.setAttribute('id', 'EV_bgModeAlertDiv');
    document.body.appendChild(bgObj);
    //背景框满窗口显示   
    EV_Show_bgDiv();
    //把要显示的div居中显示   
    EV_MsgBox_ID = msgID;
    EV_Show_msgDiv();
}
//关闭对话窗口   
function EV_closeAlert() {
    var msgObj = document.getElementById(EV_MsgBox_ID);
    var bgObj = document.getElementById("EV_bgModeAlertDiv");
    msgObj.style.display = "none";
    document.body.removeChild(bgObj);
    EV_MsgBox_ID = "";
}

//窗口大小改变时更正显示大小和位置   
window.onresize = function () {
    if (EV_MsgBox_ID.length > 0) {
        EV_Show_bgDiv();
        EV_Show_msgDiv();
    }
}

var originTop;
var originalHeight;
var zoomRatio;
var originalParametersInitialized = false;
function resetCarouselMargin() {
    var msgObj = document.getElementById(EV_MsgBox_ID);
    var curHeigh = $(msgObj).height();
    zoomRatio = curHeigh / originalHeight;
    msgObj.style.marginTop = 0 * zoomRatio + 'px';
}


//窗口滚动条拖动时更正显示大小和位置   
window.onscroll = function () {
    if (EV_MsgBox_ID.length > 0) {
        EV_Show_bgDiv();
        //EV_Show_msgDiv();
    }
}

function getOriginalCarouselMarginTop(top, height) {
    originTop = top;
    originalHeight = height;
    originalParametersInitialized = true;
}

//把要显示的div居中显示   
function EV_Show_msgDiv() {
    var msgObj = document.getElementById(EV_MsgBox_ID);
    var caroucel = document.getElementById('viewCarousel');
    msgObj.style.visibility = 'visible';
    msgObj.style.display = "block";
    if (screenHeight > screenWidth) {
        if (caroucel) {
            caroucel.style.height = screenHeight + 'px';
        }
        msgObj.style.width = screenWidth + 'px';
        msgObj.style.height = screenHeight + 'px';
    } else {

    }
    var msgWidth = msgObj.scrollWidth;
    var msgHeight = msgObj.scrollHeight;
    var bgTop = EV_myScrollTop();
    var bgLeft = EV_myScrollLeft();
    var bgWidth = EV_myClientWidth();
    var bgHeight = EV_myClientHeight();
    var msgTop = bgTop + Math.round((bgHeight - msgHeight) / 2);
    //var msgLeft = (screenWidth - $(msgObj).width()) / 2;
    var msgLeft = bgLeft + Math.round((bgWidth - msgWidth) / 2);
    msgObj.style.position = "absolute";
    msgObj.style.top = (bgTop + 10) + "px";
    msgObj.style.left = msgLeft + "px";
    msgObj.style.zIndex = "10001";
    if (!originalParametersInitialized)
        getOriginalCarouselMarginTop(msgTop, $(msgObj).height());
    else
        resetCarouselMargin();
}
//背景框满窗口显示   
function EV_Show_bgDiv() {
    var bgObj = document.getElementById("EV_bgModeAlertDiv");
    var bgWidth = EV_myClientWidth();
    var bgHeight = EV_myClientHeight();
    var bgTop = EV_myScrollTop();
    var bgLeft = EV_myScrollLeft();
    bgObj.style.position = "absolute";
    bgObj.style.top = bgTop + "px";
    bgObj.style.left = bgLeft + "px";
    bgObj.style.width = bgWidth + "px";
    bgObj.style.height = bgHeight + "px";
    bgObj.style.zIndex = "10000";
    bgObj.style.background = "#fff";
    bgObj.style.filter = "progid:DXImageTransform.Microsoft.Alpha(style=0,opacity=60,finishOpacity=60);";
    bgObj.style.opacity = "0.8";
}
//网页被卷去的上高度   
function EV_myScrollTop() {
    var n = window.pageYOffset
        || document.documentElement.scrollTop
        || document.body.scrollTop || 0;
    return n;
}
//网页被卷去的左宽度   
function EV_myScrollLeft() {
    var n = window.pageXOffset
        || document.documentElement.scrollLeft
        || document.body.scrollLeft || 0;
    return n;
}
//网页可见区域宽   
function EV_myClientWidth() {
    var n = document.documentElement.clientWidth
        || document.body.clientWidth || 0;
    return n;
}
//网页可见区域高   
function EV_myClientHeight() {
    var n = document.documentElement.clientHeight
        || document.body.clientHeight || 0;
    return n;
}

// 上传文件
var uploadFile = function (file, path, callback) {
    var Key = path + "/" + prevFile.indexOf(file) + '.jpg'; // 这里指定上传目录和文件名
    getAuthorization({ Method: 'PUT', Pathname: '/' + Key }, function (err, info) {
        if (err) {
            alert(err);
            return;
        }
        var auth = info.Authorization;
        var XCosSecurityToken = info.XCosSecurityToken;
        var url = (prefix + camSafeUrlEncode(Key)).replace(/%2F/, '/');
        var xhr = new XMLHttpRequest();
        xhr.open('PUT', url, false);
        xhr.setRequestHeader('Authorization', auth);
        xhr.setRequestHeader('Cache-Control', 'public, max-age = 0');
        XCosSecurityToken && xhr.setRequestHeader('x-cos-security-token', XCosSecurityToken);
        //xhr.setRequestHeader('q-header-list', "host");
        xhr.onreadystatechange = function () {
            if (xhr.status === 200 || xhr.status === 206) {
                var ETag = xhr.getResponseHeader('etag');
                if (prevFile.indexOf(file) == prevfile.length - 1 && imgs_failed_upload == 0) {
                    cansubmit = true;
                }
                callback(null, { url: url, ETag: ETag });
            } else {
                imgs_failed_upload += 1;
                callback('文件 ' + Key + ' 上传失败，状态码：' + xhr.status);
            }
        };
        xhr.onerror = function () {
            callback('文件 ' + Key + ' 上传失败，请检查是否没配置 CORS 跨域规则');
        };
        xhr.send(file);
    });
};
// 对更多字符编码的 url encode 格式
var camSafeUrlEncode = function (str) {
    return encodeURIComponent(str)
        .replace(/!/g, '%21')
        .replace(/'/g, '%27')
        .replace(/\(/g, '%28')
        .replace(/\)/g, '%29')
        .replace(/\*/g, '%2A');
};


/*主页代码*/
function switch_tab(obj) {
    var recommend = document.getElementById('tabList');
    var lis = recommend.getElementsByTagName('li');
    for (i = 0; i < lis.length; i++) {
        lis[i].classList.remove('li_tab_active');
    }
    obj.classList.add('li_tab_active');
}

var tbl_body_hot = document.getElementById('hottest_stories');
var tbl_body_latest = document.getElementById('latest_stories');
function load_latest(obj) {
    switch_tab(obj);
    tbl_body_hot.style.visibility = 'collapse';
    tbl_body_latest.style.visibility = 'visible';
}

function load_hotest(obj) {
    switch_tab(obj);
    tbl_body_hot.style.visibility = 'visible';
    tbl_body_latest.style.visibility = 'collapse';
}

function load_random(obj) {
    switch_tab(obj);

}