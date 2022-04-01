// pages/login/login.js
Page({

  /**
   * 页面的初始数据
   */
  data: {
    m_UseName: "",
    m_Password: "",
    btnLoginDisabled: false,
    loginText:"登录",
    errMsg:""
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
    var that2 = this
    var that = this.data
    this.setData({
      btnLoginDisabled: true,
      loginText:"登录中..."
    })
    wx.login({
      success(res) {
        if (res.code != "") {
          //发起网络请求
          var reqTask = wx.request({
            url: getApp().globalData.baseUrl + 'mini/miniapplogin/login',
            data: {
              userName: that.m_UseName,
              password: that.m_Password,
              openId: res.code
            },
            header: { 'content-type': 'application/json' },
            method: 'GET',
            dataType: 'json',
            responseType: 'text',
            success: (result) => {
              console.log(result)
              if(result.statusCode==401){
                console.log(result.data)
                that2.setData({
                  errMsg:result.data
                })
              }else if (result.data.openId != "") {
                getApp().globalData.openId = result.data.openId
                wx.redirectTo({
                  url: '/pages/home/home',
                })
              }else{
                console.log("wrong")
              }
            },
            fail: (f) => {
              console.log(f)
              this.setData({
                loginText:"登录",
                btnLoginDisabled: false
              })
            },
            complete: () => {
                that2.setData({
                  btnLoginDisabled:false,
                  loginText:"登录"
                })
            }
          });
        } else {
          console.log('登录失败！' + res.errMsg)
        }
      },
      fail(err) {
        console.log(err)
      }
    })



  }
})