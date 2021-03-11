// pages/login/login.js
Page({

  /**
   * 页面的初始数据
   */
  data: {
    m_UseName: "",
    m_Password: ""
  },

  handleUseNameInput: function (e) {
    var useName = e.detail.value;
    this.setData({
      m_UseName: useName
    })
  },

  handlepwdInput: function (e) {
    var pwd = e.detail.value;
    this.setData({
      m_Password: pwd
    })
  },

  handleLogin: function (e) {
var that=this.data
    wx.login({
      success (res) {
        if (res.code) {
          //发起网络请求
          var reqTask = wx.request({
            url: getApp().globalData.baseUrl + 'mini/miniapplogin/login',
            data: {
              userName: that.m_UseName,
              password: that.m_Password,
              openId:res.code
            },
            header: { 'content-type': 'application/json' },
            method: 'GET',
            dataType: 'json',
            responseType: 'text',
            success: (result) => {
              if(result.data.openId!=""){
                getApp().globalData.openId = result.data.openId
                wx.redirectTo({
                  url: '/pages/home/home',
                })
              }
            },
            fail: () => { },
            complete: () => { }
          });
        } else {
          console.log('登录失败！' + res.errMsg)
        }
      }
    })



  }
})