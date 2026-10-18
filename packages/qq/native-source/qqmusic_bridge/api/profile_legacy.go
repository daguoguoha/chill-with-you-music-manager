package api

import (
	"encoding/json"
	"fmt"
	"qqmusic_bridge/models"
	"strconv"
	"strings"
)

type legacyPlaylist struct {
	ID       json.RawMessage `json:"dissid"`
	TID      json.RawMessage `json:"tid"`
	Name     string          `json:"title"`
	DissName string          `json:"dissname"`
	Picture  string          `json:"picurl"`
	Logo     string          `json:"logo"`
	Subtitle string          `json:"subtitle"`
	SongNum  int             `json:"songnum"`
}
type legacyProfile struct {
	Creator struct {
		Nick    string `json:"nick"`
		HeadPic string `json:"headpic"`
	} `json:"creator"`
	MyDiss struct {
		Num  int              `json:"num"`
		List []legacyPlaylist `json:"list"`
	} `json:"mydiss"`
}

func (c *Client) legacyGet(path string, params map[string]string, target interface{}) error {
	resp, err := c.httpClient.R().SetHeader("Cookie", c.GetCookies()).SetQueryParams(params).Get("https://c.y.qq.com" + path)
	if err != nil {
		return fmt.Errorf("profile network request failed")
	}
	if resp.StatusCode() != 200 {
		return fmt.Errorf("profile HTTP status %d", resp.StatusCode())
	}
	if err = json.Unmarshal(resp.Body(), target); err != nil {
		return fmt.Errorf("profile response could not be read")
	}
	return nil
}
func (c *Client) fetchProfile() (*legacyProfile, error) {
	c.mu.RLock()
	uin, gtk := c.uin, c.gtk
	c.mu.RUnlock()
	if uin == 0 {
		return nil, fmt.Errorf("not logged in")
	}
	var result struct {
		Code int           `json:"code"`
		Data legacyProfile `json:"data"`
	}
	err := c.legacyGet("/rsc/fcgi-bin/fcg_get_profile_homepage.fcg", map[string]string{
		"g_tk": strconv.Itoa(gtk), "format": "json", "inCharset": "utf8", "outCharset": "utf-8", "notice": "0", "platform": "yqq.json", "needNewCode": "0",
		"cid": "205360838", "userid": strconv.FormatInt(uin, 10), "reqfrom": "1", "reqtype": "0", "hostUin": "0", "loginUin": strconv.FormatInt(uin, 10),
	}, &result)
	if err != nil {
		return nil, err
	}
	if result.Code != 0 {
		return nil, fmt.Errorf("profile API code %d", result.Code)
	}
	return &result.Data, nil
}
func (c *Client) fetchProfileUser() (*models.UserInfo, error) {
	profile, err := c.fetchProfile()
	if err != nil {
		return nil, err
	}
	user := &models.UserInfo{UIN: c.GetUIN(), Nickname: profile.Creator.Nick, AvatarUrl: profile.Creator.HeadPic}
	c.SetUserInfo(user)
	return user, nil
}
func convertLegacy(items []legacyPlaylist) []models.PlaylistInfo {
	result := make([]models.PlaylistInfo, 0, len(items))
	for _, item := range items {
		raw := item.ID
		if len(raw) == 0 {
			raw = item.TID
		}
		id, _ := strconv.ParseInt(strings.Trim(string(raw), "\""), 10, 64)
		if id <= 0 {
			continue
		}
		name := item.Name
		if name == "" {
			name = item.DissName
		}
		cover := item.Picture
		if cover == "" {
			cover = item.Logo
		}
		count := item.SongNum
		if count == 0 {
			fmt.Sscanf(item.Subtitle, "%d", &count)
		}
		result = append(result, models.PlaylistInfo{DissID: id, DissName: name, SongCount: count, CoverUrl: cover})
	}
	return result
}
func (c *Client) fetchProfilePlaylists() ([]models.PlaylistInfo, error) {
	profile, err := c.fetchProfile()
	if err != nil {
		return nil, err
	}
	return convertLegacy(profile.MyDiss.List), nil
}
func (c *Client) fetchCollectedLegacy() ([]models.PlaylistInfo, error) {
	c.mu.RLock()
	uin, gtk := c.uin, c.gtk
	c.mu.RUnlock()
	var result struct {
		Code   int              `json:"code"`
		CDList []legacyPlaylist `json:"cdlist"`
	}
	err := c.legacyGet("/fav/fcgi-bin/fcg_get_profile_order_asset.fcg", map[string]string{
		"g_tk": strconv.Itoa(gtk), "format": "json", "inCharset": "utf8", "outCharset": "utf-8", "notice": "0", "platform": "yqq.json", "needNewCode": "0",
		"cid": "205360956", "userid": strconv.FormatInt(uin, 10), "reqtype": "3", "sin": "0", "ein": "199",
	}, &result)
	if err != nil {
		return nil, err
	}
	if result.Code != 0 {
		return nil, fmt.Errorf("collected playlist API code %d", result.Code)
	}
	return convertLegacy(result.CDList), nil
}
