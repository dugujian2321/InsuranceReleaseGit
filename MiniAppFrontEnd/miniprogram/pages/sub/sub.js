//var base64 = require("../images/base64");
Page({
  /**
 * 页面的初始数据
 */
  data: {
    calculated: false,
    error: '',
    list_add: [],
    temp_list: [],
    showCalendar: false,
    startDate: {},
    str_StartDate: "尚未选择",
    date: "点此选择",
    company_list: [],
    selected_company: "",
    plan: [],
    selected_plan: "",
    ServerMsg: [],
    price: '0',
    CalculateText: "计算保费",
    calculateBtnEnabled: true,
    SubmitText: "提交",
    scanBtnDisabled: false,
    employee_info: '',
    bSearchDisabled: false,

    showSearchList: 'none',
    searchResultList: [],
    selectedPeopleList: [],
    test: ["1", "2", "3", "3", "3", "3", "3", "3"]
  },
  onLoad: function () {
    var reqTask = wx.request({
      url: getApp().globalData.baseUrl + 'mini/miniappemployeechange/miniemployeechange?openId=' + getApp().globalData.openId,
      data: {},
      header: { 'content-type': 'application/json' },
      method: 'GET',
      dataType: 'json',
      responseType: 'text',
      success: (result) => {
        this.setData({
          company_list: result.data.companyNameList,
        })
        var plans = result.data.plans
        if (result.data.plans == "all") {
          plans = ["60万","60万A","60万B", "80万A", "80万B"],
            this.setData({
              selected_plan: plans[0],
            })
        } else {
          plans = [result.data.plans],
            this.setData({
              selected_plan: plans,
            })
        }
        this.setData({
          plan: plans,
          selected_company: this.data.company_list[0]
        })
      },
      fail: () => { },
      complete: () => { }
    });
    this.setData({
      //icon: base64.icon20,
      slideButtons: [{
        type: 'warn',
        text: '删除',
        extClass: 'test',
        //src: '/page/weui/cell/icon_del.svg', // icon的路径
      }],
    });
  },
  handleInfoInput: function (e) {
    console.log(e)
    this.setData({
      employee_info: e.detail.value
    })
  },
  addToSUbList: function (e) {
    var resultArray = this.data.list_add
    for (var i = 0; i < this.data.selectedPeopleList.length; i++) {
      var index = this.data.selectedPeopleList[i]
      var temp = {
        name: this.data.searchResultList[index].name,
        id: this.data.searchResultList[index].id,
        job: this.data.searchResultList[index].job,
        jobtype: this.data.searchResultList[index].type
      }
      this.data.searchResultList[index].isChecked = "False"
      resultArray.push(temp)
    }

    this.setData({
      list_add: resultArray,
      selectedPeopleList: [],
      searchResultList: this.data.searchResultList
    })
    console.log(this.data.list_add)
  },
  checkboxChange: function (e) {
    //console.log(e)
    this.setData({
      selectedPeopleList: e.detail.value
    })
  },
  handleSearch: function (e) {
    console.log(e)
    this.setData({
      searchResultList: [],
      bSearchDisabled: true
    })
    var info = {
      companyName: this.data.selected_company,
      content: this.data.employee_info
    }
    console.log(info)
    var reqTask = wx.request({
      url: getApp().globalData.baseUrl + 'mini/miniappemployeechange/minisearchpeople?companyName=' +
        this.data.selected_company + '&content=' + this.data.employee_info + '&openId=' + getApp().globalData.openId,
      header: { 'content-type': 'application/json' },
      method: 'POST',
      dataType: 'json',
      responseType: 'text',
      success: (result) => {
        var tempMsg = []
        console.log(result)
        this.setData({
          showSearchList: '',
          searchResultList: result.data,
        })

        console.log(this.data.searchResultList)
      },
      fail: (f) => {
        console.log(f)
      },
      complete: () => {
        this.setData({
          bSearchDisabled: false

        })
      }
    });
  },
  uncalculate: function () {
    this.setData({
      calculated: false,
      price: '0'
    })
  },
  bindCompanyChange: function (e) {
    //console.log(e)
    this.setData({
      selected_company: this.data.company_list[e.detail.value]
    })
    this.uncalculate()
  },
  bindPlanChange: function (e) {
    this.setData({
      selected_plan: this.data.plan[e.detail.value],
    })
    this.uncalculate()
  },
  bindDateChange: function (e) {
    this.setData({
      date: e.detail.value,
    })
    this.uncalculate()
  },
  selectDate: function () {
    this.setData({
      showCalendar: !this.data.showCalendar
    })
  },
  afterTapDate: function (e) {
    this.setData({
      startDate: {
        year: e.detail.year,
        month: e.detail.month,
        day: e.detail.date
      }
    })
    this.setData({
      str_StartDate: this.data.startDate.year + "年" + this.data.startDate.month + "月" + this.data.startDate.day + "日"
    })

    this.selectDate()
    console.log(this.data.str_StartDate)
  },
  changeCalculateBtnState: function (e) {
    if (this.data.CalculateText == "计算保费") {
      this.setData({
        CalculateText: "计算中...",
        calculateBtnEnabled: false,
        scanBtnDisabled: true
      })
    } else {
      this.setData({
        CalculateText: "计算保费",
        calculateBtnEnabled: true,
        scanBtnDisabled: false
      })
    }

  },
  changeSubmitBtnState: function (e) {
    if (this.data.SubmitText == "提交") {
      this.setData({
        SubmitText: "正在提交...",
        calculated: false,
        calculateBtnEnabled: false,
        scanBtnDisabled: true
      })
    } else {
      this.setData({
        SubmitText: "提交",
        calculated: true,
        calculateBtnEnabled: true,
        scanBtnDisabled: false
      })
    }
  },
  calculate: function (e) {
    this.setData({
      showSearchList: 'none',
      bSearchDisabled: true,
      calculated: false
    })
    var that = this
    // var debugList = [
    //   {
    //     name: '胡晓东',
    //     id: '32072119890505163X',
    //     jobtype: 'jt1',
    //     job: 'j1'
    //   }
    // ]
    // this.setData({
    //   list_add: debugList
    // })
    if (this.data.list_add.length <= 0) {
      this.showErrorMsg("人员列表为空")
      return
    }
    var dataobj = {
      personList: JSON.stringify(this.data.list_add),
      mode1: "sub",
      date: this.data.date,
      company: this.data.selected_company,
      plan: this.data.selected_plan,
      openId: getApp().globalData.openId
    }
    this.changeCalculateBtnState()
    var reqTask = wx.request({
      url: getApp().globalData.baseUrl + 'mini/miniappemployeechange/minicalculateprice',
      data: dataobj,
      header: { 'content-type': 'application/x-www-form-urlencoded' },
      method: 'POST',
      dataType: 'json',
      responseType: 'text',
      success: (result) => {
        var tempMsg = []
        if (Array.isArray(result.data)) {
          for (var i = 0; i < result.data.length; i++) {
            console.log(result.data[i])
            tempMsg.push(result.data[i])
          }
          this.setData({
            ServerMsg: tempMsg
          })
          this.popup = this.selectComponent("#popup"),
            this.popup.showPopup();
          return
        } else {
          if (result.data.indexOf("无误") == -1) {
            tempMsg.push(result.data)
            this.setData({
              ServerMsg: tempMsg
            })
            this.popup = this.selectComponent("#popup"),
              this.popup.showPopup();
            return
          }
        }
        var str_price = result.data.replace("数据无误", "")
        this.setData({
          price: str_price
        })
        this.setData({
          calculated: true
        })
        this.showErrorMsg("保费计算完成")
      },
      fail: (f) => {
        console.log(f)
      },
      complete: () => {
        this.changeCalculateBtnState()
        this.setData({
          bSearchDisabled: false
        })
      }
    });
  },
  success: function (e) {
    var newAdd = {
      name: e.detail.name.text,
      id: e.detail.id.text,
      jobtype: '',
      job: ''
    }
    this.data.temp_list.push(newAdd)
    this.setData({
      list_add: this.data.temp_list
    })
    this.uncalculate()
  },
  _success: function (e) {
    this.popup = this.selectComponent("#popup")
    this.popup.hidePopup();
  },
  slideButtonTap(e) {
    this.setData({
      temp_list: this.data.list_add
    })

    var i = e.currentTarget.dataset.index;
    var t = this.data.temp_list.splice(i, 1)

    this.setData({
      list_add: this.data.temp_list
    })
    this.uncalculate()
  },
  showErrorMsg: function (e) {
    this.setData({
      error: e
    })
  },

  submit(e) {
    if (this.data.list_add.length <= 0) {
      console.log("no data")
      return
    }
    this.setData({
      bSearchDisabled: true
    })
    this.changeSubmitBtnState()
    var reqTask = wx.request({
      url: getApp().globalData.baseUrl + 'mini/miniappemployeechange/MiniUpdateSummary?date=' + this.data.date +
        '&plan=' + this.data.selected_plan +
        '&openId=' + getApp().globalData.openId,
      data: {
      },
      header: { 'content-type': 'application/json' },
      method: 'GET',
      dataType: 'json',
      responseType: 'text',

      success: (result) => {
        console.log(result)
        var tempMsg = []
        if (Array.isArray(result.data)) {
          if (result.data[0] == "退保成功") {

            tempMsg.push("退保成功")
            tempMsg.push("结束时间：" + result.data[2])
            tempMsg.push("本次退保人数：" + result.data[3])
            tempMsg.push("本次保费：" + result.data[4])

            this.setData({
              ServerMsg: tempMsg,
              list_add: []
            })
            this.popup = this.selectComponent("#popup")
            this.popup.showPopup();
            this.uncalculate()
            return
          }
        }
      },
      fail: (f) => {
        console.log(f)
      },
      complete: () => {
        var su = this.data.calculated
        this.changeSubmitBtnState()
        this.setData({
          bSearchDisabled: false
        })
        if (su == false) {
          this.setData({
            calculated: false
          })
        }
      }
    });
  }
});